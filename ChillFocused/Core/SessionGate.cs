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

            if (!timer.Known || !timer.Running)
            {
                return false;
            }

            // Working is what the game calls a work phase; a break phase is running
            // too, but it must not freeze anything.
            if (!timer.Working)
            {
                return false;
            }

            var window = freshSeconds > 0f ? freshSeconds : DefaultFreshSeconds;
            return now - timer.ChangedAt <= window;
        }
    }
}
