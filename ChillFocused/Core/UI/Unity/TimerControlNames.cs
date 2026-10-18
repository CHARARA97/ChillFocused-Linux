namespace ChillFocused.Core
{
    /// <summary>
    /// The game objects the timer lock hides. Names are data, kept in one file: when a
    /// game update renames a button, this is the only place that changes.
    /// </summary>
    /// <remarks>
    /// Verified against game 1.17.3 by the <c>timer-lock: hid …</c> log line. The
    /// extra names are fallbacks for builds that name the same control differently;
    /// an unmatched name costs one string compare and is otherwise harmless.
    /// </remarks>
    internal static class TimerControlNames
    {
        /// <summary>Play/pause of the running timer, one object per mode.</summary>
        internal static readonly string[] Pause =
        {
            "PomodoroPlayOrPauseButton", "CountupPlayOrPauseButton",
        };

        /// <summary>"Skip this phase": this build calls it PomodoroNextButton.</summary>
        internal static readonly string[] Skip = { "PomodoroNextButton", "SkipButton" };

        /// <summary>The control that abandons the whole Pomodoro plan.</summary>
        internal static readonly string[] Stop = { "PomodoroResetButton" };
    }
}
