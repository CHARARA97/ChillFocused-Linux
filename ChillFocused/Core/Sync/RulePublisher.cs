using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Sends this plugin's rule set when it changes, when it has not been sent for a
    /// while, or when Focused restarted (new pid).
    /// </summary>
    /// <remarks>
    /// Kept separate from the session heartbeat: a rejected rule set must not stop
    /// the lease renewal. The actual request is a delegate so retry/backoff can be
    /// tested without a socket.
    /// </remarks>
    public sealed class RulePublisher
    {
        private readonly Action<string> _send;
        private readonly Action<string> _warn;
        private readonly Action<string> _log;
        private readonly RuleSync _sync = new RuleSync();
        private FocusSettings _last;
        private string _lastError;

        public RulePublisher(Action<string> send, Action<string> warn, Action<string> log)
        {
            _send = send ?? (body => { });
            _warn = warn ?? (message => { });
            _log = log ?? (message => { });
        }

        public int PushCount { get; private set; }

        public string LastError
        {
            get { return _lastError; }
        }

        public int BackendPid { get; private set; }

        public bool IsDue(float now)
        {
            return _sync.Due(now);
        }

        public void Publish(FocusSettings settings, float now, int? backendPid)
        {
            settings = settings ?? new FocusSettings();

            if (backendPid.HasValue)
            {
                BackendPid = backendPid.Value;
                _sync.ObservedPid(backendPid.Value);
            }

            if (!settings.RulesEqual(_last))
            {
                _last = settings;
                _sync.SettingsChanged();
            }

            if (!_sync.Due(now))
            {
                return;
            }

            try
            {
                _send(settings.BuildRulesJson());
                _sync.Sent();
                PushCount++;
                _lastError = null;
                _log(LogEvent.Line("sync.rules", "summary=" + Describe(settings)));
            }
            catch (Exception ex)
            {
                _sync.Failed(now);
                var message = ex.GetType().Name + ": " + ex.Message;
                if (!string.Equals(message, _lastError, StringComparison.Ordinal))
                {
                    _lastError = message;
                    _warn(LogEvent.Line("sync.rules.failed",
                            "reason=" + message, "heartbeat=continues"));
                }
            }
        }

        private static string Describe(FocusSettings settings)
        {
            return RuleText.Describe(settings.ProcessNames, 8) + " (names), " +
                   RuleText.Describe(settings.CmdlineSubstrings, 8) + " (cmdline)";
        }
    }
}
