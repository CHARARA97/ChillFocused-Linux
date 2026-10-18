using System;

namespace ChillFocused.Core
{
    /// <summary>Builds and sends one session heartbeat with its lease.</summary>
    internal sealed class SessionReporter
    {
        private readonly Action<string> _send;

        public SessionReporter(Action<string> send)
        {
            _send = send ?? (body => { });
        }

        /// <summary>Send the decision and return it, so the caller can log transitions.</summary>
        public bool Report(FocusSettings settings, TimerState timer, float now,
                           bool? activeOverride = null)
        {
            settings = settings ?? new FocusSettings();
            var active = activeOverride ?? settings.SessionActive(timer, now);
            var body =
                "{\"active\":" + (active ? "true" : "false") +
                ",\"dry_run\":" + (settings.DryRun ? "true" : "false") +
                ",\"source\":\"game-timer\"" +
                ",\"ttl_seconds\":" + settings.SessionLeaseSeconds + "}";
            _send(body);
            return active;
        }
    }
}
