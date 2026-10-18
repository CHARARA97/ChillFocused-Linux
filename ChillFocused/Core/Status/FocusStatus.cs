namespace ChillFocused.Core
{
    /// <summary>
    /// One immutable snapshot of everything the UI and panel need to know about Focused.
    /// </summary>
    /// <remarks>
    /// The runner replaces the whole snapshot on every transition instead of
    /// mutating a dozen fields. A reader that captured a snapshot therefore keeps a
    /// coherent view while the next poll builds a new one.
    /// </remarks>
    internal sealed class FocusStatus
    {
        internal FocusStatus()
            : this(false, false, false, "?", false, false, false, 0, 0,
                   new string[0], new string[0], default(TimerState), string.Empty)
        {
        }

        internal FocusStatus(
            bool connected,
            bool dryRun,
            bool degraded,
            string version,
            bool gateEnabled,
            bool gateSatisfied,
            bool sessionActive,
            int frozenCount,
            int frozenTotal,
            string[] frozenNames,
            string[] recentNames,
            TimerState timer,
            string lastWarning)
        {
            Connected = connected;
            DryRun = dryRun;
            Degraded = degraded;
            Version = string.IsNullOrEmpty(version) ? "?" : version;
            GateEnabled = gateEnabled;
            GateSatisfied = gateSatisfied;
            SessionActive = sessionActive;
            FrozenCount = frozenCount;
            FrozenTotal = frozenTotal;
            FrozenNames = Clone(frozenNames);
            RecentNames = Clone(recentNames);
            Timer = timer;
            LastWarning = lastWarning ?? string.Empty;
        }

        public bool Connected { get; private set; }
        public bool DryRun { get; private set; }
        public bool Degraded { get; private set; }
        public string Version { get; private set; }
        public bool GateEnabled { get; private set; }
        public bool GateSatisfied { get; private set; }
        public bool SessionActive { get; private set; }
        public int FrozenCount { get; private set; }
        public int FrozenTotal { get; private set; }
        public string[] FrozenNames { get; private set; }
        public string[] RecentNames { get; private set; }
        public TimerState Timer { get; private set; }
        public string LastWarning { get; private set; }

        /// <summary>The Focused manual session, when one exists.</summary>
        public string SessionMode { get; private set; }
        public string SessionPhase { get; private set; }
        public float SessionRemaining { get; private set; }
        public float SessionElapsed { get; private set; }
        public int SessionCycles { get; private set; }
        public bool SessionManual { get; private set; }

        internal FocusStatus WithTimer(TimerState timer)
        {
            return new FocusStatus(Connected, DryRun, Degraded, Version, GateEnabled,
                GateSatisfied, SessionActive, FrozenCount, FrozenTotal,
                FrozenNames, RecentNames, timer, LastWarning);
        }

        internal FocusStatus WithWarning(string warning)
        {
            return new FocusStatus(Connected, DryRun, Degraded, Version, GateEnabled,
                GateSatisfied, SessionActive, FrozenCount, FrozenTotal,
                FrozenNames, RecentNames, Timer, warning);
        }

        internal FocusStatus WithSession(string mode, string phase, float remaining,
                                        float elapsed, int cycles, bool manual)
        {
            var copy = new FocusStatus(
                Connected, DryRun, Degraded, Version, GateEnabled, GateSatisfied,
                SessionActive, FrozenCount, FrozenTotal, FrozenNames, RecentNames,
                Timer, LastWarning)
            {
                SessionMode = mode ?? string.Empty,
                SessionPhase = phase ?? string.Empty,
                SessionRemaining = remaining < 0f ? 0f : remaining,
                SessionElapsed = elapsed < 0f ? 0f : elapsed,
                SessionCycles = cycles < 0 ? 0 : cycles,
                SessionManual = manual,
            };
            return copy;
        }

        internal FocusStatus WithGate(bool enabled, bool satisfied)
        {
            return new FocusStatus(Connected, DryRun, Degraded, Version, enabled,
                satisfied, SessionActive, FrozenCount, FrozenTotal,
                FrozenNames, RecentNames, Timer, LastWarning);
        }

        internal FocusStatus Disconnected()
        {
            return new FocusStatus(false, DryRun, false, Version, GateEnabled,
                false, false, FrozenCount, FrozenTotal,
                FrozenNames, RecentNames, Timer, LastWarning);
        }

        private static string[] Clone(string[] values)
        {
            return values == null ? new string[0] : (string[])values.Clone();
        }
    }
}
