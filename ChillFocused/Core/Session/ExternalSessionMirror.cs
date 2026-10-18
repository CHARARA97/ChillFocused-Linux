using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Mirrors a Focused manual session onto the game's timer.
    /// </summary>
    /// <remarks>
    /// The mirror is the only component that decides when to call ITimerController
    /// and when to suppress the normal game-timer heartbeat. It is deliberately
    /// small: policy lives in SessionMirrorPolicy, game calls in GameTimerController.
    /// </remarks>
    internal sealed class ExternalSessionMirror
    {
        private readonly ITimerController _controller;
        private readonly ISessionReportGate _gate;
        private readonly Func<FocusSettings> _settingsProvider;
        private readonly Action _stopExternalSession;
        private readonly Action<string> _log;
        private bool _applying;
        private bool _endingExternal;
        private float _retryStopAt;
        private int _stopAttempts;
        private float _endingSince;
        private float _lastTimeSyncAt;
        private bool _timeSyncFailedLogged;

        private SessionOwnership _owner = SessionOwnership.Game;
        private SessionPlan _lastPlan;
        private float _externalSince;
        private bool _unsupportedLogged;

        internal ExternalSessionMirror(ITimerController controller, ISessionReportGate gate,
                                       Func<FocusSettings> settingsProvider,
                                       Action stopExternalSession, Action<string> log)
        {
            _controller = controller;
            _gate = gate;
            _settingsProvider = settingsProvider ?? (() => new FocusSettings());
            _stopExternalSession = stopExternalSession ?? (() => { });
            _log = log ?? (message => { });
        }

        internal void Observe(ExternalSessionState state)
        {
            if (_endingExternal)
            {
                if (state == null || !state.Active || !state.Manual)
                {
                    _endingExternal = false;
                }
                else if (state.Since > 0f && _endingSince > 0f
                         && Math.Abs(state.Since - _endingSince) > 0.01f)
                {
                    // A new Focused session started while the previous stop was
                    // still in flight. Its `since` is different, so it is not a
                    // stale response; let the policy see it.
                    _endingExternal = false;
                }
                else if (GameClock.Now < _retryStopAt)
                {
                    // The stop POST is in flight. Do not start a new game timer
                    // from a stale active=true response.
                    return;
                }
                else if (_stopAttempts >= 3)
                {
                    // Focused did not end after repeated requests. Stop treating
                    // every active response as stale, or a new session would be
                    // suppressed forever.
                    _endingExternal = false;
                }
                else
                {
                    _stopAttempts++;
                    _stopExternalSession();
                    _retryStopAt = GameClock.Now + 2f;
                    return;
                }
            }

            var settings = _settingsProvider() ?? new FocusSettings();
            var decision = SessionMirrorPolicy.Decide(
                settings.MirrorExternalSession,
                _owner,
                _lastPlan,
                state);

            switch (decision.Action)
            {
                case SessionMirrorAction.Start:
                    _gate.EnterExternal();
                    if (ApplyPlan(decision.Plan))
                    {
                        _lastPlan = decision.Plan;
                        _owner = SessionOwnership.External;
                        _externalSince = state != null ? state.Since : 0f;
                        _unsupportedLogged = false;
                        _endingExternal = false;
                        _stopAttempts = 0;
                        _endingSince = 0f;
                        _lastTimeSyncAt = 0f;
                        _timeSyncFailedLogged = false;
                        _log(LogEvent.Line("session.mirror.start", "mode=" + decision.Plan.Mode,
                            "work=" + decision.Plan.WorkMinutes,
                            "cycles=" + decision.Plan.Cycles));
                    }
                    else
                    {
                        _gate.LeaveExternal();
                        _owner = SessionOwnership.Game;
                        _lastPlan = null;
                    }
                    break;

                case SessionMirrorAction.Apply:
                    // A countdown's remaining seconds shrink every poll; that is
                    // not a new plan. Reapplying would rewrite the game's minutes
                    // once per minute. Keep the plan captured at session start.
                    if (state != null && state.Mode == ExternalSessionMode.Countdown
                        && Math.Abs(state.Since - _externalSince) < 0.01f)
                    {
                        break;
                    }

                    if (ApplyPlan(decision.Plan))
                    {
                        _lastPlan = decision.Plan;
                        _externalSince = state != null ? state.Since : 0f;
                        _log(LogEvent.Line("session.mirror.plan", "mode=" + decision.Plan.Mode,
                            "work=" + decision.Plan.WorkMinutes,
                            "cycles=" + decision.Plan.Cycles));
                    }
                    break;

                case SessionMirrorAction.Stop:
                    Stop(decision.Reason, state);
                    break;
            }

            if (_owner == SessionOwnership.External)
            {
                var force = decision.Action == SessionMirrorAction.Start
                            || decision.Action == SessionMirrorAction.Apply;
                SyncClock(state, settings, force);
            }
        }

        internal void ObserveGameTimer(TimerState state)
        {
            if (_applying || _owner != SessionOwnership.External || !state.Known || state.Running)
            {
                return;
            }

            // The user stopped the in-game Pomodoro while Focused owned it.
            // End the Focused manual session so it cannot keep the external
            // session alive or restart the game timer on the next poll.
            _endingExternal = true;
            _retryStopAt = GameClock.Now + 2f;
            _stopAttempts = 0;
            _endingSince = _externalSince;
            _stopExternalSession();
            _gate.LeaveExternal();
            _owner = SessionOwnership.Game;
            _lastPlan = null;
            _externalSince = 0f;
            _unsupportedLogged = false;
            _lastTimeSyncAt = 0f;
            _timeSyncFailedLogged = false;
            _stopAttempts = 0;
            _log(LogEvent.Line("session.mirror.stop", "action=game-stop"));
        }

        private bool ApplyPlan(SessionPlan plan)
        {
            if (!_controller.CanControl)
            {
                if (!_unsupportedLogged)
                {
                    _unsupportedLogged = true;
                    _log(LogEvent.Line("session.mirror.unsupported", "reason=no-controller"));
                }

                return false;
            }

            _applying = true;
            TimerControlResult result;
            try
            {
                result = _controller.Apply(plan);
            }
            finally
            {
                _applying = false;
            }

            if (result.Status == TimerControlStatus.Applied)
            {
                return true;
            }

            _log(LogEvent.Line("session.mirror.failed",
                "status=" + result.Status,
                "reason=" + (result.Message ?? "unknown")));
            return false;
        }

        private void SyncClock(ExternalSessionState state, FocusSettings settings, bool force)
        {
            var interval = settings.MirrorTimeSyncSeconds;
            if (interval <= 0f || state == null || !state.Active || !state.Manual)
            {
                return;
            }

            var now = GameClock.Now;
            if (!force && now - _lastTimeSyncAt < interval)
            {
                return;
            }

            _lastTimeSyncAt = now;

            float seconds;
            if (state.Mode == ExternalSessionMode.Countup)
            {
                seconds = state.ElapsedSeconds;
            }
            else if (state.Mode == ExternalSessionMode.Countdown
                     || state.Mode == ExternalSessionMode.Pomodoro)
            {
                seconds = state.RemainingSeconds;
            }
            else
            {
                return;
            }

            if (seconds <= 0f)
            {
                return;
            }

            _applying = true;
            TimerControlResult result;
            try
            {
                result = _controller.SyncTime(
                    state.Mode == ExternalSessionMode.Countup ? TimerMode.Countup : TimerMode.Pomodoro,
                    seconds);
            }
            finally
            {
                _applying = false;
            }

            if (result.Status != TimerControlStatus.Applied && !_timeSyncFailedLogged)
            {
                _timeSyncFailedLogged = true;
                _log(LogEvent.Line("session.mirror.sync.failed",
                    "mode=" + state.Mode,
                    "status=" + result.Status,
                    "reason=" + (result.Message ?? "unknown")));
            }
        }

        private void Stop(string reason, ExternalSessionState state)
        {
            if (reason == "external-ended")
            {
                // Focused's stop endpoint is asynchronous. Ending it here prevents
                // the next poll from treating a stale active session as a new
                // one. This is idempotent when Focused already ended it.
                _endingExternal = true;
                _retryStopAt = GameClock.Now + 2f;
                _stopAttempts = 0;
                _endingSince = state != null && state.Since > 0f ? state.Since : _externalSince;
                _stopExternalSession();
            }

            _applying = true;
            TimerControlResult result;
            try
            {
                result = _controller.Reset();
            }
            finally
            {
                _applying = false;
            }
            if (result.Status != TimerControlStatus.Applied)
            {
                _log(LogEvent.Line("session.mirror.stop.failed",
                    "status=" + result.Status,
                    "reason=" + (result.Message ?? "unknown")));
            }

            _gate.LeaveExternal();
            _owner = SessionOwnership.Game;
            _lastPlan = null;
            _externalSince = 0f;
            _unsupportedLogged = false;
            _lastTimeSyncAt = 0f;
            _timeSyncFailedLogged = false;
            _stopAttempts = 0;
            _log(LogEvent.Line("session.mirror.stop",
                "action=Reset",
                "reason=" + reason,
                "active=" + (state != null && state.Active),
                "manual=" + (state != null && state.Manual),
                "since=" + (state != null ? state.Since : 0f)));
        }
    }
}
