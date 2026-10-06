namespace ChillFocused.Core
{
    /// <summary>
    /// Decides whether a focus session is running, from what the plugin knows.
    /// </summary>
    /// <remarks>
    /// This used to live in the outside executor: the plugin reported the game's
    /// timer and the executor applied the window. Now that the plugin talks to
    /// Focused directly, the plugin owns the decision -- it is the only side that
    /// sees the game at all -- and Focused is told the answer, as a lease it has to
    /// keep hearing.
    ///
    /// Unity-free and pure on purpose: this is the rule the whole feature hangs on,
    /// so it is unit tested rather than reasoned about.
    /// </remarks>
    internal static class SessionGate
    {
        /// <summary>How long a timer reading stays believable.</summary>
        /// <remarks>
        /// The game stops writing when it is paused or closed; without a window the
        /// last reading ("working") would keep a session open forever.
        /// </remarks>
        public const float DefaultFreshSeconds = 25f;

        public static bool Active(
            bool enabled,
            bool alwaysOn,
            TimerState timer,
            float now,
            float freshSeconds)
        {
            if (!enabled)
            {
                return false;
            }

            if (alwaysOn)
            {
                return true;
            }

            if (!timer.Known)
            {
                return false;
            }

            // The game states the phase outright (Work / Break / Complete), which is
            // more informative than the booleans: a break and a completed phase both
            // report "not working", and neither should freeze anything, while a pause
            // keeps the phase at Work with the timer stopped.
            var phase = timer.Phase ?? string.Empty;
            var working = timer.Working || string.Equals(phase, "Work", System.StringComparison.Ordinal);

            if (!working)
            {
                return false;
            }

            if (!timer.Running)
            {
                // Paused mid-work: the phase still says Work, so this is not "the
                // session ended", it is "the game stopped counting".
                return false;
            }

            var window = freshSeconds > 0f ? freshSeconds : DefaultFreshSeconds;

            // Freshness is about the last *reading*, not the last change: a steady
            // work phase reports identical values for its whole length, and measuring
            // from the last change expired every session after the window while the
            // game was still counting.  States without a ReadAt fall back to the old
            // field so the rule stays total.
            var lastReading = timer.ReadAt > 0f ? timer.ReadAt : timer.ChangedAt;
            return now - lastReading <= window;
        }
    }
}
