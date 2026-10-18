using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The timer-control lock: which game buttons are hidden, per timer mode.
    /// </summary>
    /// <remarks>
    /// The count-up case is the one that must never regress: its stop button is the
    /// only way out of the mode, so hiding it traps the player.
    /// </remarks>
    public class TimerControlPlanTests
    {
        private static TimerState Timer(TimerMode mode, bool running = true, bool known = true,
                                        bool resting = false, bool working = true)
        {
            return new TimerState
            {
                Known = known,
                Mode = mode,
                Running = running,
                Working = working && !resting,
                Resting = resting,
                ChangedAt = 100f,
                ReadAt = 100f,
            };
        }

        [Fact]
        public void DisabledSwitchHidesNothing()
        {
            var plan = TimerControlPlan.From(false, Timer(TimerMode.Pomodoro));

            Assert.False(plan.Any);
        }

        [Fact]
        public void UnknownTimerHidesNothing()
        {
            var plan = TimerControlPlan.From(true, Timer(TimerMode.Unknown, known: false));

            Assert.False(plan.Any);
        }

        [Fact]
        public void IdleTimerHidesNothing()
        {
            var plan = TimerControlPlan.From(true, Timer(TimerMode.Pomodoro, running: false));

            Assert.False(plan.Any);
        }

        [Fact]
        public void CompletedPlanHidesNothing()
        {
            // The game keeps IsTimerRunning true after a plan completes; only the phase
            // flags say whether anything is still counting.
            var plan = TimerControlPlan.From(
                true, Timer(TimerMode.Pomodoro, running: true, working: false));

            Assert.False(plan.Any);
        }

        [Fact]
        public void PomodoroWorkHidesPauseSkipAndStop()
        {
            var plan = TimerControlPlan.From(true, Timer(TimerMode.Pomodoro));

            Assert.True(plan.HidePause);
            Assert.True(plan.HideSkip);
            Assert.True(plan.HideStop);
        }

        [Fact]
        public void PomodoroBreakHidesTheSameControls()
        {
            var plan = TimerControlPlan.From(true, Timer(TimerMode.Pomodoro, resting: true));

            Assert.True(plan.HidePause);
            Assert.True(plan.HideSkip);
            Assert.True(plan.HideStop);
        }

        [Fact]
        public void CountupHidesPauseButKeepsStop()
        {
            var plan = TimerControlPlan.From(true, Timer(TimerMode.Countup));

            Assert.True(plan.HidePause);
            Assert.False(plan.HideSkip);
            Assert.False(plan.HideStop);
        }
    }
}
