using System;
using System.Reflection;

namespace ChillFocused.Core
{
    /// <summary>
    /// Writes the game's Pomodoro/count-up timer through the services the game
    /// already exposes.
    /// </summary>
    /// <remarks>
    /// This is the only class that knows the game's method names. Applying a plan
    /// intentionally changes the game's saved Pomodoro settings: that is the
    /// product behaviour, not a temporary override. No Unity or BepInEx type is
    /// exposed to the application layer.
    /// </remarks>
    internal sealed class GameTimerController : ITimerController
    {
        private readonly object _sync = new object();
        private readonly Action<string> _log;
        private object _pomodoro;
        private object _countup;
        private object _pomodoroTimer;
        private object _countupTimer;
        private bool _loggedPomodoroMissing;
        private bool _loggedCountupMissing;
        private TimerMode _activeMode = TimerMode.Unknown;

        internal GameTimerController(Action<string> log = null)
        {
            _log = log;
        }

        public bool CanControl
        {
            get
            {
                lock (_sync)
                {
                    return ResolvePomodoro() != null || ResolveCountup() != null;
                }
            }
        }

        public TimerControlResult Apply(SessionPlan plan)
        {
            if (plan == null)
            {
                return TimerControlResult.Unsupported("empty plan");
            }

            lock (_sync)
            {
                TimerControlResult result;
                try
                {
                    if (plan.Mode == TimerMode.Pomodoro)
                    {
                        result = ApplyPomodoro(plan);
                    }
                    else if (plan.Mode == TimerMode.Countup)
                    {
                        result = ApplyCountup();
                    }
                    else
                    {
                        result = TimerControlResult.Unsupported("unknown timer mode");
                    }
                }
                catch (Exception ex)
                {
                    result = TimerControlResult.Failed(ex.GetType().Name + ": " + ex.Message);
                }

                if (result.Status != TimerControlStatus.Applied)
                {
                    // The cached service may belong to a scene that has since been
                    // unloaded. Let the next poll pick up the current instance.
                    ClearCachedServices();
                }

                return result;
            }
        }

        public TimerControlResult SyncTime(TimerMode mode, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                return TimerControlResult.Unsupported("invalid time");
            }

            lock (_sync)
            {
                try
                {
                    if (mode == TimerMode.Countup)
                    {
                        var service = ResolveCountupTimer();
                        var set = service == null ? null : Find(service, "SetElapsedTime", 1);
                        if (set == null)
                        {
                            return TimerControlResult.Unsupported("countup clock setter unavailable");
                        }

                        Invoke(set, service, new object[] { TimeSpan.FromSeconds(seconds) });
                        return TimerControlResult.Applied();
                    }

                    if (mode == TimerMode.Pomodoro)
                    {
                        var service = ResolvePomodoroTimer();
                        var set = service == null ? null : Find(service, "SetRemainingTime", 1);
                        if (set == null)
                        {
                            return TimerControlResult.Unsupported("pomodoro clock setter unavailable");
                        }

                        Invoke(set, service, new object[] { TimeSpan.FromSeconds(seconds) });
                        return TimerControlResult.Applied();
                    }

                    return TimerControlResult.Unsupported("unknown timer mode");
                }
                catch (Exception ex)
                {
                    ClearCachedServices();
                    return TimerControlResult.Failed(ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        public TimerControlResult Reset()
        {
            lock (_sync)
            {
                try
                {
                    var service = _activeMode == TimerMode.Countup
                        ? ResolveCountup()
                        : ResolvePomodoro();
                    if (service == null)
                    {
                        ClearCachedServices();
                        return TimerControlResult.Unsupported("timer service unavailable");
                    }

                    var reset = Find(service, "ResetTimer", 0);
                    if (reset != null)
                    {
                        Invoke(reset, service, null);
                    }
                    else
                    {
                        // CountupService.ResetTimer(bool) is the only overload in
                        // the current game build; false means a normal reset.
                        var resetWithFlag = Find(service, "ResetTimer", 1);
                        if (resetWithFlag == null)
                        {
                            ClearCachedServices();
                            return TimerControlResult.Unsupported("ResetTimer unavailable");
                        }

                        Invoke(resetWithFlag, service, new object[] { true });
                    }

                    _activeMode = TimerMode.Unknown;
                    return TimerControlResult.Applied();
                }
                catch (Exception ex)
                {
                    ClearCachedServices();
                    return TimerControlResult.Failed(ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private TimerControlResult ApplyPomodoro(SessionPlan plan)
        {
            var service = ResolvePomodoro();
            if (service == null)
            {
                return TimerControlResult.Unsupported("PomodoroService unavailable");
            }

            var setWork = Find(service, "SetWorkMinutes", 1);
            var setBreak = Find(service, "SetBreakMinutes", 1);
            var setLoops = Find(service, "SetLoopCount", 1);
            var start = Find(service, "StartPomodoro", 0);
            if (setWork == null || setBreak == null || setLoops == null || start == null)
            {
                return TimerControlResult.Unsupported("Pomodoro setters unavailable");
            }

            if (_activeMode != TimerMode.Pomodoro)
            {
                // The game starts with the Pomodoro UI selected. Reset it even
                // when this is the first external session of the process.
                var countup = ResolveCountup();
                ResetCountup(countup);
            }

            Invoke(setWork, service, new object[] { plan.WorkMinutes });
            Invoke(setBreak, service, new object[] { plan.BreakMinutes });
            Invoke(setLoops, service, new object[] { plan.Cycles });
            // Match the game's own flow: switch the parent UI first, then start.
            ChangeTimerUi("ChangePomodoroTimer");
            Invoke(start, service, null);
            _activeMode = TimerMode.Pomodoro;
            return TimerControlResult.Applied();
        }

        private TimerControlResult ApplyCountup()
        {
            var service = ResolveCountup();
            if (service == null)
            {
                return TimerControlResult.Unsupported("CountupService unavailable");
            }

            if (_activeMode != TimerMode.Countup)
            {
                // Symmetric with ApplyPomodoro: clear the other mode even when
                // we do not know what the game had selected before us.
                var pomodoro = ResolvePomodoro();
                var reset = pomodoro == null ? null : Find(pomodoro, "ResetTimer", 0);
                if (reset != null)
                {
                    Invoke(reset, pomodoro, null);
                }
            }

            var start = Find(service, "StartCountup", 0)
                        ?? Find(service, "PlayCountupTimer", 0);
            if (start == null)
            {
                return TimerControlResult.Unsupported("Countup start unavailable");
            }

            // Match the game's own flow: switch the parent UI first, then start.
            ChangeTimerUi("ChangeCountupTimer");
            Invoke(start, service, null);
            _activeMode = TimerMode.Countup;
            return TimerControlResult.Applied();
        }

        private static void ResetCountup(object countup)
        {
            if (countup == null)
            {
                return;
            }

            var reset = Find(countup, "ResetTimer", 0);
            if (reset != null)
            {
                Invoke(reset, countup, null);
                return;
            }

            var resetWithFlag = Find(countup, "ResetTimer", 1);
            if (resetWithFlag != null)
            {
                Invoke(resetWithFlag, countup, new object[] { true });
            }
        }

        private object ResolvePomodoroTimer()
        {
            if (_pomodoroTimer == null)
            {
                _pomodoroTimer = GameTimerProbe.PomodoroTimerService
                                 ?? GameServices.Resolve("Bulbul.PomodoroTimerService");
            }

            return _pomodoroTimer;
        }

        private object ResolveCountupTimer()
        {
            if (_countupTimer == null)
            {
                _countupTimer = GameTimerProbe.CountupTimerService
                                ?? GameServices.Resolve("Bulbul.CountupTimerService");
            }

            return _countupTimer;
        }

        private object ResolvePomodoro()
        {
            if (_pomodoro == null)
            {
                var probe = GameTimerProbe.PomodoroService;
                var di = probe == null ? GameServices.Resolve("Bulbul.PomodoroService") : null;
                _pomodoro = probe ?? di;
                if (_pomodoro == null && !_loggedPomodoroMissing)
                {
                    _loggedPomodoroMissing = true;
                    if (_log != null)
                    {
                        _log(LogEvent.Line("session.timer.missing", "service=PomodoroService",
                            "probe=false", "di=false",
                            "reason=" + GameServices.LastFailure));
                    }
                }
            }

            return _pomodoro;
        }

        private object ResolveCountup()
        {
            if (_countup == null)
            {
                var probe = GameTimerProbe.CountupService;
                var di = probe == null ? GameServices.Resolve("Bulbul.CountupService") : null;
                _countup = probe ?? di;
                if (_countup == null && !_loggedCountupMissing)
                {
                    _loggedCountupMissing = true;
                    if (_log != null)
                    {
                        _log(LogEvent.Line("session.timer.missing", "service=CountupService",
                            "probe=false", "di=false",
                            "reason=" + GameServices.LastFailure));
                    }
                }
            }

            return _countup;
        }

        private void ChangeTimerUi(string methodName)
        {
            var parent = GameTimerProbe.TimerParentUI;
            if (parent == null)
            {
                return;
            }

            try
            {
                var method = Find(parent, methodName, 0);
                if (method != null)
                {
                    Invoke(method, parent, null);
                }
            }
            catch (Exception ex)
            {
                // The timer has started; a UI switch failure must not undo it.
                if (_log != null)
                {
                    _log(LogEvent.Line("session.timer.ui.failed", "method=" + methodName,
                        "reason=" + ex.GetType().Name + ":" + ex.Message));
                }
            }
        }

        private void ClearCachedServices()
        {
            _pomodoro = null;
            _countup = null;
            _pomodoroTimer = null;
            _countupTimer = null;
        }

        private static MethodInfo Find(object instance, string name, int parameterCount)
        {
            if (instance == null)
            {
                return null;
            }

            var methods = instance.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == name
                    && methods[i].GetParameters().Length == parameterCount)
                {
                    return methods[i];
                }
            }

            return null;
        }

        private static object Invoke(MethodInfo method, object instance, object[] args)
        {
            return method.Invoke(instance, args);
        }
    }
}
