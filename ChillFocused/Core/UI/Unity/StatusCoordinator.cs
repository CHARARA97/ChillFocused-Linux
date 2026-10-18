using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Owns the focused-status snapshot, poller cursor and connection transitions.
    /// </summary>
    /// <remarks>
    /// The Unity runner keeps the frame loop and user actions. This coordinator keeps
    /// the state machine: connected/disconnected, gate calculation, warning text and
    /// timer updates. It is still Unity-side because the presenter pushes into IMGUI,
    /// but the runner no longer sequences those transitions inline.
    /// </remarks>
    internal sealed class StatusCoordinator
    {
        private readonly StatusPresenter _presenter;
        private readonly Action<string> _warn;
        private readonly Action _requestImmediateSync;
        private readonly Action<ExternalSessionState> _onExternalSession;
        private readonly StatusPoller _poller = new StatusPoller();
        private FocusStatus _status = new FocusStatus();
        private IFocusedEndpoint _client;
        private string _lastLoggedError;

        internal StatusCoordinator(StatusPresenter presenter, Action<string> warn,
                                   Action requestImmediateSync,
                                   Action<ExternalSessionState> onExternalSession = null)
        {
            _presenter = presenter;
            _warn = warn ?? (message => { });
            _requestImmediateSync = requestImmediateSync ?? (() => { });
            _onExternalSession = onExternalSession;
        }

        internal FocusStatus Status
        {
            get { return _status; }
        }

        internal StatusPoller Poller
        {
            get { return _poller; }
        }

        internal void SetClient(IFocusedEndpoint client)
        {
            _client = client;
            _poller.SetFeed(client);
        }

        internal void Poll(float now, float interval, FocusSettings settings, Action onConnected)
        {
            if (_client == null)
            {
                return;
            }

            _poller.Poll(now, interval,
                delegate(FocusStateResponse status, string error)
                {
                    if (error != null || status == null || !status.ok)
                    {
                        if (_onExternalSession != null)
                        {
                            _onExternalSession(ExternalSessionState.Inactive());
                        }

                        Disconnected(error ?? "unexpected response");
                        return;
                    }

                    if (_onExternalSession != null)
                    {
                        _onExternalSession(ExternalSessionMapper.From(status));
                    }

                    var wasConnected = _status.Connected;
                    Connected(status, settings);
                    if (!wasConnected && onConnected != null)
                    {
                        onConnected();
                    }
                },
                delegate(EventListResponse list)
                {
                    // The poller owns the cursor; future event toasts hook in here.
                });
        }

        internal void OnTimer(TimerState state)
        {
            _status = _status.WithTimer(state);
            _requestImmediateSync();
        }

        internal void SetWarning(string message)
        {
            var warning = (message ?? string.Empty).Trim();
            if (warning.Length > 200)
            {
                warning = warning.Substring(0, 200);
            }

            _status = _status.WithWarning(warning);
            _presenter.Warning(warning);
        }

        private void Connected(FocusStateResponse response, FocusSettings settings)
        {
            var previous = _status;
            if (!previous.Connected)
            {
                _lastLoggedError = null;
                _poller.BeginEvents();
            }

            _status = FocusStatusMapper.Apply(_status, response);
            _status = _status.WithGate(!settings.AlwaysOn, _status.SessionActive);
            _presenter.Connected(previous, _status, settings,
                _client != null ? _client.BaseUrl : string.Empty,
                !string.IsNullOrEmpty(response.version));
        }

        private void Disconnected(string reason)
        {
            var wasConnected = _status.Connected;
            _status = _status.Disconnected();
            _poller.ResetEvents();
            _presenter.Disconnected(_status, _client != null ? _client.BaseUrl : string.Empty);

            if (wasConnected || _lastLoggedError != reason)
            {
                _lastLoggedError = reason;
                SetWarning("Focused unreachable: " + reason);
                _warn(LogEvent.Line("sync.failed", "reason=" + reason));
            }
        }
    }
}
