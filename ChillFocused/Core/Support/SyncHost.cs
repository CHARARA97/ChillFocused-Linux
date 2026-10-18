using System;

namespace ChillFocused.Core
{
    /// <summary>Builds and owns the background sync thread and its transport.</summary>
    internal sealed class SyncHost
    {
        private readonly Func<FocusSettings> _settingsProvider;
        private readonly ITimerSource _timerSource;
        private readonly ISessionReportGate _reportGate;
        private readonly Action _onCycle;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private HeadlessRunner _sync;

        internal SyncHost(RuntimeHostContext context)
        {
            _settingsProvider = context.SettingsProvider;
            _timerSource = context.TimerSource;
            _reportGate = context.ReportGate;
            _onCycle = context.OnCycle;
            _logInfo = context.LogInfo;
            _logWarning = context.LogWarning;
            _logError = context.LogError;
        }

        internal HeadlessRunner Sync
        {
            get { return _sync; }
        }

        internal void Start(FocusSettings settings)
        {
            if (_sync != null)
            {
                return;
            }

            try
            {
                var transport = new HttpTransport(
                    settings.FocusedUrl, settings.FocusedToken, settings.RequestTimeoutMs);
                var probe = new FocusStatusProbe(
                    () => transport.Request("GET", "/api/v1/focus", null));
                var rules = new RulePublisher(
                    body => transport.Request("PUT", "/api/v1/focus/rules", body),
                    message => _logWarning("[sync] " + message),
                    message => _logInfo("[sync] " + message));
                var reporter = new SessionReporter(
                    body => transport.Request("POST", "/api/v1/focus/state", body));

                _sync = new HeadlessRunner(
                    _settingsProvider,
                    transport,
                    probe,
                    rules,
                    reporter,
                    2.0,
                    message => _logInfo("[sync] " + message),
                    message => _logWarning("[sync] " + message),
                    _reportGate);
                _sync.OnCycle = _onCycle;
                _sync.GameStateProvider = _timerSource.Snapshot;
                _sync.Start();
            }
            catch (Exception ex)
            {
                _logError("could not start the sync thread: " + ex);
            }
        }

        internal void Wake()
        {
            var sync = _sync;
            if (sync != null)
            {
                sync.Wake();
            }
        }

        internal void Fire(string method, string path, string body)
        {
            var sync = _sync;
            if (sync != null)
            {
                sync.Fire(method, path, body);
            }
        }
    }
}
