namespace ChillFocused.Core
{
    /// <summary>
    /// Which of the game's timer controls must be hidden right now.
    /// </summary>
    /// <remarks>
    /// Pure decision: the switch plus the game's own timer reading go in, three
    /// booleans come out. Keeping it here (and not in the Unity guard) is what makes
    /// the mode rules testable without the game running.
    ///
    /// The rules differ per mode on purpose:
    /// <list type="bullet">
    /// <item>Pomodoro has a planned length, so pause, skip and stop all go away for
    /// both work and break phases -- skipping the plan is exactly what the switch is
    /// meant to prevent.</item>
    /// <item>Countup has no planned length and its stop button is the only way back
    /// out of the mode; hiding it would trap the player, so only pause is hidden.</item>
    /// </list>
    /// </remarks>
    internal struct TimerControlPlan
    {
        /// <summary>Hide the play/pause control of the running timer.</summary>
        public bool HidePause;

        /// <summary>Hide "skip this phase" / "next".</summary>
        public bool HideSkip;

        /// <summary>Hide the control that abandons the whole plan.</summary>
        public bool HideStop;

        internal bool Any
        {
            get { return HidePause || HideSkip || HideStop; }
        }

        /// <summary>
        /// Plans only while a phase is actually counting (work or break). <c>Running</c>
        /// alone is not enough: the game keeps <c>PomodoroService.IsTimerRunning</c> true
        /// after a plan completes, so gating on it left the buttons hidden for good.
        /// </summary>
        internal static TimerControlPlan From(bool enabled, TimerState timer)
        {
            var plan = new TimerControlPlan();
            var counting = timer.Running && (timer.Working || timer.Resting);
            if (!enabled || !timer.Known || !counting)
            {
                return plan;
            }

            plan.HidePause = true;

            if (timer.Mode == TimerMode.Pomodoro)
            {
                plan.HideSkip = true;
                plan.HideStop = true;
            }

            return plan;
        }

        /// <summary>Short form for the log, so the guard does not rebuild the wording.</summary>
        internal string Describe()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (HidePause) parts.Add("pause");
            if (HideSkip) parts.Add("skip");
            if (HideStop) parts.Add("stop");
            return "hide=" + string.Join(",", parts.ToArray());
        }
    }
}
