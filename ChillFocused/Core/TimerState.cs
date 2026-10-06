namespace ChillFocused.Core
{
    /// <summary>What the game's own timer services say, right now.</summary>
    /// <remarks>
    /// Deliberately free of any Unity or Harmony types. <see cref="HeadlessRunner"/>
    /// consumes this through a delegate, which is what lets the runner stay under
    /// the test project -- whose whole point is that the logic there compiles
    /// without the game, Unity or Harmony present.
    /// </remarks>
    internal struct TimerState
    {
        /// <summary>False until the game has told us anything.</summary>
        public bool Known;

        /// <summary>A work phase is running (either timer kind).</summary>
        public bool Working;

        /// <summary>The timer is running at all.</summary>
        public bool Running;

        /// <summary>Which hook produced this reading, for the log.</summary>
        public string Source;

        /// <summary>Unscaled time of the reading.</summary>
        public float ChangedAt;

        /// <summary>When this state was last *read* from the game.</summary>
        /// <remarks>
        /// Different from <see cref="ChangedAt"/> on purpose: a work phase reports the
        /// same values for its whole length, so "the last time anything changed" is
        /// useless as a liveness signal -- measuring freshness from it made every
        /// steady session look stale after the window (25 s by default) and turned the
        /// session off while the game was still counting.
        /// </remarks>
        public float ReadAt;

        /// <summary>The game's phase: "Work", "Break", "Complete", or empty if unknown.</summary>
        /// <remarks>
        /// The game exposes this directly (PomodoroService.CurrentPomodoroType), which
        /// removes the guesswork: the booleans say "working" or "not working", while the
        /// phase says *why* -- a break, a completed phase, or a pause (phase Work with
        /// IsTimerRunning false).
        /// </remarks>
        public string Phase;

        /// <summary>True while the game reports its break phase.</summary>
        public bool Resting;

        public bool SameAs(TimerState other)
        {
            return Known == other.Known && Working == other.Working && Running == other.Running
                   && Resting == other.Resting && string.Equals(Phase, other.Phase, System.StringComparison.Ordinal);
        }
    }
}
