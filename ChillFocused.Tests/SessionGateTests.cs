using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The session decision, which used to live in the outside executor.
    /// </summary>
    /// <remarks>
    /// Every case here is one that would otherwise suspend somebody's work -- or
    /// fail to suspend what they asked to be kept away from.
    /// </remarks>
    public class SessionGateTests
    {
        private static TimerState Timer(bool working, bool running = true, bool known = true,
                                        float changedAt = 100f)
        {
            return new TimerState
            {
                Known = known,
                Working = working,
                Running = running,
                Source = "test",
                ChangedAt = changedAt,
            };
        }

        [Fact]
        public void A_fresh_working_reading_starts_a_session()
        {
            Assert.True(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: Timer(true), now: 110f, freshSeconds: 25f));
        }

        [Fact]
        public void The_master_switch_wins_over_everything()
        {
            Assert.False(SessionGate.Active(
                enabled: false, alwaysOn: true, timer: Timer(true), now: 110f, freshSeconds: 25f));
        }

        [Fact]
        public void Always_on_ignores_the_timer_entirely()
        {
            Assert.True(SessionGate.Active(
                enabled: true, alwaysOn: true, timer: default(TimerState), now: 110f,
                freshSeconds: 25f));
        }

        [Fact]
        public void A_break_phase_is_running_but_not_working()
        {
            Assert.False(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: Timer(false), now: 110f, freshSeconds: 25f));
        }

        [Fact]
        public void A_stale_reading_ends_the_session()
        {
            // The game stopped writing (paused, closed, or its timer idled): the last
            // "working" must not keep a session open forever.
            Assert.False(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: Timer(true, changedAt: 100f), now: 126f,
                freshSeconds: 25f));
        }

        [Fact]
        public void An_unknown_timer_never_starts_a_session()
        {
            Assert.False(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: default(TimerState), now: 110f,
                freshSeconds: 25f));
        }

        [Fact]
        public void A_stopped_timer_never_starts_a_session()
        {
            Assert.False(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: Timer(true, running: false), now: 110f,
                freshSeconds: 25f));
        }

        [Fact]
        public void A_zero_window_falls_back_to_the_default()
        {
            Assert.True(SessionGate.Active(
                enabled: true, alwaysOn: false, timer: Timer(true, changedAt: 100f), now: 120f,
                freshSeconds: 0f));
        }
    }
}
