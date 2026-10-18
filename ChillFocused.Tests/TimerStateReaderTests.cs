using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    internal sealed class FakeCore
    {
        public bool IsCurrentWorking() { return true; }
        public bool IsCurrentResting() { return false; }
    }

    internal sealed class FakePomodoro
    {
        public bool Running;
        public string Phase;
        public bool IsTimerRunning() { return Running; }
        public bool IsCurrentWorking() { return Running; }
        public bool IsCurrentResting() { return false; }
        public string CurrentPomodoroType { get { return Phase; } }
    }

    internal sealed class FakePomodoroTimer
    {
        private bool _isRunning;
        public bool Running { set { _isRunning = value; } }
    }

    internal sealed class FakeCountup
    {
        public bool Running;
        public bool Working = true;
        public bool IsTimerRunning() { return Running; }
        public bool IsCurrentWorking() { return Working; }
    }

    public class TimerStateReaderTests
    {
        [Fact]
        public void A_running_countup_wins_over_pomodoro()
        {
            string raw;
            var state = TimerStateReader.Read(
                new FakeCore(), new FakePomodoro { Running = true, Phase = "Work" }, null,
                new FakeCountup { Running = true }, null, "test", 12f, out raw);

            Assert.Equal(TimerMode.Countup, state.Mode);
            Assert.True(state.Known);
            Assert.True(state.Working);
            Assert.True(state.Running);
            Assert.Equal(12f, state.ReadAt);
        }

        [Fact]
        public void Core_running_false_overrides_a_stale_pomodoro_running_flag()
        {
            string raw;
            var state = TimerStateReader.Read(
                new PausedCore(), new FakePomodoro { Running = true, Phase = "Work" },
                new FakePomodoroTimer { Running = false }, null, null, "test", 5f, out raw);

            Assert.Equal(TimerMode.Pomodoro, state.Mode);
            Assert.False(state.Running);
            Assert.True(state.Working);
        }

        private sealed class PausedCore
        {
            public bool IsTimerRunning() { return false; }
            public bool IsCurrentWorking() { return true; }
            public bool IsCurrentResting() { return false; }
        }

        [Fact]
        public void A_paused_countup_is_not_active()
        {
            string raw;
            var state = TimerStateReader.Read(
                new FakeCore(), null, null,
                new FakeCountup { Running = true, Working = false }, null,
                "CountupService.Pause", 8f, out raw);

            Assert.Equal(TimerMode.Countup, state.Mode);
            Assert.False(state.Working);
            Assert.False(state.Running);
        }

        [Fact]
        public void A_pomodoro_phase_is_preserved()
        {
            string raw;
            var state = TimerStateReader.Read(
                new FakeCore(), new FakePomodoro { Running = true, Phase = "Work" }, null,
                null, null, "test", 5f, out raw);

            Assert.Equal(TimerMode.Pomodoro, state.Mode);
            Assert.Equal("Work", state.Phase);
            Assert.True(state.Running);
        }

        [Fact]
        public void Missing_services_fail_closed_to_unknown()
        {
            string raw;
            var state = TimerStateReader.Read(null, null, null, null, null, "test", 1f, out raw);

            Assert.Equal(TimerMode.Unknown, state.Mode);
            Assert.False(state.Known);
            Assert.False(state.Running);
        }
    }
}
