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

        public bool SameAs(TimerState other)
        {
            return Known == other.Known && Working == other.Working && Running == other.Running;
        }
    }
}
