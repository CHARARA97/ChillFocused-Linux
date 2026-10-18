namespace ChillFocused.Core
{
    /// <summary>
    /// When the plugin must send its rule set to the executor again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Focused holds the pushed rules in memory only, and deliberately so: it must never
    /// resume enforcing a rule set nobody re-sent. A restart therefore leaves it with just
    /// its own config file, which makes the re-push load-bearing rather than an
    /// optimisation. The background sync thread already re-sends everything every 30
    /// seconds as a backstop; what this policy adds is the overlay path noticing a restart
    /// immediately instead of waiting for that refresh, and never adopting a rule change
    /// it does not send.
    /// </para>
    /// <para>
    /// A restart is detected from the executor's own pid, which every status response
    /// carries. That is more reliable than watching for a failed request: Focused that
    /// comes back between two polls never produces one, and a connection that comes back
    /// without the process changing needs no re-push at all.
    /// </para>
    /// <para>
    /// Unity-free on purpose, and the clock arrives as an argument, so the whole policy is
    /// unit tested.
    /// </para>
    /// </remarks>
    public sealed class RuleSync
    {
        /// <summary>How long to wait after a failed push before trying again.</summary>
        public const float RetrySeconds = 5f;

        // The first rule set goes out as soon as there is a connection to send it to.
        private bool _due = true;
        private float _retryAt;
        private int _executorPid;

        /// <summary>True when the rules should be sent now.</summary>
        public bool Due(float now)
        {
            return _due && now >= _retryAt;
        }

        /// <summary>The executor accepted the rule set.</summary>
        public void Sent()
        {
            _due = false;
        }

        /// <summary>The push failed; the next attempt waits out the backoff.</summary>
        public void Failed(float now)
        {
            _due = true;
            _retryAt = now + RetrySeconds;
        }

        /// <summary>The rules themselves changed -- a config edit, or a panel edit.</summary>
        public void SettingsChanged()
        {
            _due = true;
            _retryAt = 0f;
        }

        /// <summary>
        /// A status came back from the executor, which reports the pid of the process that
        /// answered. A different pid than last time means the executor restarted and lost
        /// whatever was pushed to it.
        /// </summary>
        public void ObservedPid(int pid)
        {
            if (pid <= 0)
            {
                // No pid to compare (an older executor, or a status we could not parse).
                return;
            }

            if (_executorPid > 0 && pid != _executorPid)
            {
                _due = true;
                _retryAt = 0f;
            }

            _executorPid = pid;
        }
    }
}
