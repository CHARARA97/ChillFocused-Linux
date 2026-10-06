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
                                        float changedAt = 100f, float readAt = 0f,
                                        string phase = null, bool resting = false)
        {
            return new TimerState
            {
                Known = known,
                Working = working,
                Running = running,
                ReadAt = readAt,
                Phase = phase,
                Resting = resting,
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
        public void A_steady_work_phase_does_not_expire()
        {
            // The regression: a work phase reports the same values for its whole
            // length, so ChangedAt stops moving.  Freshness has to follow the last
            // reading instead, or the session turns off after the window while the
            // game is still counting.
            var timer = Timer(working: true, changedAt: 100f, readAt: 990f);

            Assert.True(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
        }

        [Fact]
        public void A_break_phase_releases_even_though_the_timer_runs()
        {
            // Phase Break with the timer still running: the game is on a break, so the
            // apps come back -- the documented behaviour.
            var timer = Timer(working: false, running: true, phase: "Break", resting: true,
                              changedAt: 990f, readAt: 995f);

            Assert.False(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
        }

        [Fact]
        public void A_completed_phase_is_not_a_session()
        {
            var timer = Timer(working: false, running: true, phase: "Complete",
                              changedAt: 990f, readAt: 995f);

            Assert.False(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
        }

        [Fact]
        public void A_paused_work_phase_is_not_a_session_yet()
        {
            // Phase Work but the timer is stopped: paused, not ended.  Today this
            // releases (0 s of grace); the point is that it is distinguishable from a
            // break, which the phase name now makes explicit.
            var timer = Timer(working: true, running: false, phase: "Work",
                              changedAt: 990f, readAt: 995f);

            Assert.False(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
        }

        [Fact]
        public void The_phase_name_alone_can_hold_a_session_open()
        {
            // Some hooks report the phase before the booleans agree; Work wins.
            var timer = Timer(working: false, running: true, phase: "Work",
                              changedAt: 990f, readAt: 995f);

            Assert.True(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
        }

        [Fact]
        public void Readings_that_stopped_arriving_expire()
        {
            // The case the window exists for: the game is paused or gone, so nothing
            // is being read any more.
            var timer = Timer(working: true, changedAt: 100f, readAt: 100f);

            Assert.False(SessionGate.Active(true, false, timer, now: 1000f, freshSeconds: 25f));
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
