using System;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class ExternalSessionMapperTests
    {
        [Fact]
        public void Maps_countdown_fields()
        {
            var state = ExternalSessionMapper.From(new FocusStateResponse
            {
                active = true,
                manual_session = true,
                session_mode = "countdown",
                session_remaining = 1490,
                session_elapsed = 10,
                session_work_minutes = 25,
                session_cycles = 1,
            });

            Assert.True(state.Active);
            Assert.True(state.Manual);
            Assert.Equal(ExternalSessionMode.Countdown, state.Mode);
            Assert.Equal(1490, state.RemainingSeconds);
            Assert.Equal(10, state.ElapsedSeconds);
            Assert.Equal(25, state.WorkMinutes);
        }

        [Fact]
        public void Maps_pomodoro_and_stopwatch()
        {
            var pomodoro = ExternalSessionMapper.From(new FocusStateResponse
            {
                active = true, manual_session = true, session_mode = "pomodoro",
                session_work_minutes = 50, session_break_minutes = 10, session_cycles = 2,
            });
            var stopwatch = ExternalSessionMapper.From(new FocusStateResponse
            {
                active = true, manual_session = true, session_mode = "stopwatch",
            });

            Assert.Equal(ExternalSessionMode.Pomodoro, pomodoro.Mode);
            Assert.Equal(2, pomodoro.Cycles);
            Assert.Equal(ExternalSessionMode.Countup, stopwatch.Mode);
        }
    }

    public class SessionMirrorPolicyTests
    {
        [Fact]
        public void A_countdown_becomes_one_pomodoro_cycle()
        {
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.Game, null,
                Active(ExternalSessionMode.Countdown, remaining: 25 * 60));

            Assert.Equal(SessionMirrorAction.Start, decision.Action);
            Assert.Equal(TimerMode.Pomodoro, decision.Plan.Mode);
            Assert.Equal(25, decision.Plan.WorkMinutes);
            Assert.Equal(1, decision.Plan.Cycles);
        }

        [Fact]
        public void A_pomodoro_keeps_work_break_and_cycles()
        {
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.Game, null,
                Active(ExternalSessionMode.Pomodoro, work: 50, rest: 10, cycles: 2));

            Assert.Equal(50, decision.Plan.WorkMinutes);
            Assert.Equal(10, decision.Plan.BreakMinutes);
            Assert.Equal(2, decision.Plan.Cycles);
        }

        [Fact]
        public void A_stopwatch_becomes_countup()
        {
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.Game, null,
                Active(ExternalSessionMode.Countup));

            Assert.Equal(TimerMode.Countup, decision.Plan.Mode);
        }

        [Fact]
        public void The_same_plan_is_not_reapplied()
        {
            var plan = SessionPlan.Pomodoro(25, 0, 1);
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.External, plan,
                Active(ExternalSessionMode.Countdown, remaining: 25 * 60));

            Assert.Equal(SessionMirrorAction.None, decision.Action);
        }

        [Fact]
        public void Ending_an_external_session_stops_it()
        {
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.External,
                SessionPlan.Pomodoro(25, 5, 1), ExternalSessionState.Inactive());

            Assert.Equal(SessionMirrorAction.Stop, decision.Action);
        }

        [Fact]
        public void A_countdown_without_remaining_time_is_not_mapped()
        {
            var state = Active(ExternalSessionMode.Countdown, remaining: 0);
            Assert.Null(SessionPlanFactory.From(state));
        }

        [Fact]
        public void A_pomodoro_without_work_or_cycles_is_not_mapped()
        {
            var state = Active(ExternalSessionMode.Pomodoro, remaining: 600);
            state.WorkMinutes = 0;
            Assert.Null(SessionPlanFactory.From(state));

            state.WorkMinutes = 25;
            state.Cycles = 0;
            Assert.Null(SessionPlanFactory.From(state));
        }

        [Fact]
        public void Disabling_the_feature_stops_an_external_session()
        {
            var decision = SessionMirrorPolicy.Decide(
                false, SessionOwnership.External,
                SessionPlan.Pomodoro(25, 5, 1), Active(ExternalSessionMode.Countdown, remaining: 600));

            Assert.Equal(SessionMirrorAction.Stop, decision.Action);
        }

        [Fact]
        public void Unknown_mode_is_not_started()
        {
            var decision = SessionMirrorPolicy.Decide(
                true, SessionOwnership.Game, null,
                Active(ExternalSessionMode.Unknown));

            Assert.Equal(SessionMirrorAction.None, decision.Action);
        }

        private static ExternalSessionState Active(
            ExternalSessionMode mode, int work = 25, int rest = 5, int cycles = 1,
            float remaining = 0)
        {
            return new ExternalSessionState
            {
                Active = true,
                Manual = true,
                Mode = mode,
                WorkMinutes = work,
                BreakMinutes = rest,
                Cycles = cycles,
                RemainingSeconds = remaining,
            };
        }
    }

    public class SessionReportGateTests
    {
        [Fact]
        public void External_ownership_suppresses_reports_and_requests_one_withdrawal()
        {
            var gate = new SessionReportGate();
            Assert.True(gate.CanReport);

            gate.EnterExternal();
            Assert.False(gate.CanReport);
            Assert.True(gate.TryConsumeWithdrawal());
            Assert.False(gate.TryConsumeWithdrawal());

            gate.LeaveExternal();
            Assert.True(gate.CanReport);
        }
    }

    public class ExternalSessionMirrorTests
    {
        [Fact]
        public void Starts_once_and_stops_on_end()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(controller, gate, () => settings, () => { }, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600,
            });
            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600,
            });
            Assert.Equal(1, controller.Applied);
            Assert.False(gate.CanReport);

            mirror.Observe(ExternalSessionState.Inactive());
            Assert.Equal(1, controller.Resets);
            Assert.True(gate.CanReport);
        }

        [Fact]
        public void A_new_session_with_a_new_since_is_not_treated_as_stale()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var stopped = 0;
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(
                controller, gate, () => settings, () => stopped++, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600, Since = 100,
            });
            mirror.Observe(ExternalSessionState.Inactive());
            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 300, Since = 200,
            });

            Assert.Equal(2, controller.Applied);
            Assert.Equal(1, stopped);
            Assert.False(gate.CanReport);
        }

        [Fact]
        public void A_stale_active_response_does_not_restart_during_stop()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var stopped = 0;
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(
                controller, gate, () => settings, () => stopped++, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600, Since = 100,
            });
            mirror.Observe(ExternalSessionState.Inactive());
            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600, Since = 100,
            });

            Assert.Equal(1, controller.Applied);
            Assert.Equal(1, stopped);
            Assert.True(gate.CanReport);
        }

        [Fact]
        public void A_game_stop_ends_the_external_session()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var stopped = 0;
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(
                controller, gate, () => settings, () => stopped++, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600, Since = 100,
            });
            mirror.ObserveGameTimer(new TimerState
            {
                Known = true, Mode = TimerMode.Pomodoro, Running = false,
            });

            Assert.Equal(1, stopped);
            Assert.True(gate.CanReport);
        }

        [Fact]
        public void A_counting_down_session_does_not_reapply_each_minute()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(controller, gate, () => settings, () => { }, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600, Since = 100,
            });
            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 540, Since = 100,
            });

            Assert.Equal(1, controller.Applied);
        }

        [Fact]
        public void An_unsupported_controller_does_not_take_ownership()
        {
            var controller = new FakeController { Supported = false };
            var gate = new SessionReportGate();
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(controller, gate, () => settings, () => { }, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 600,
            });

            Assert.True(gate.CanReport);
            Assert.Equal(0, controller.Applied);
        }

        [Fact]
        public void A_countup_start_syncs_the_elapsed_time()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(controller, gate, () => settings, () => { }, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countup,
                ElapsedSeconds = 42,
            });

            Assert.Equal(1, controller.Synced);
            Assert.Equal(TimerMode.Countup, controller.LastSyncMode);
            Assert.Equal(42, controller.LastSyncSeconds);
        }

        [Fact]
        public void A_countdown_start_syncs_the_remaining_time()
        {
            var controller = new FakeController();
            var gate = new SessionReportGate();
            var settings = new FocusSettings { MirrorExternalSession = true };
            var mirror = new ExternalSessionMirror(controller, gate, () => settings, () => { }, _ => { });

            mirror.Observe(new ExternalSessionState
            {
                Active = true, Manual = true, Mode = ExternalSessionMode.Countdown,
                RemainingSeconds = 590, Since = 100,
            });

            Assert.Equal(1, controller.Synced);
            Assert.Equal(TimerMode.Pomodoro, controller.LastSyncMode);
            Assert.Equal(590, controller.LastSyncSeconds);
        }

        private sealed class FakeController : ITimerController
        {
            public bool Supported = true;
            public int Applied;
            public int Synced;
            public int Resets;
            public float LastSyncSeconds;
            public TimerMode LastSyncMode;
            public bool CanControl { get { return Supported; } }
            public TimerControlResult Apply(SessionPlan plan)
            {
                Applied++;
                return Supported ? TimerControlResult.Applied() : TimerControlResult.Unsupported("no");
            }
            public TimerControlResult SyncTime(TimerMode mode, float seconds)
            {
                Synced++;
                LastSyncMode = mode;
                LastSyncSeconds = seconds;
                return TimerControlResult.Applied();
            }

            public TimerControlResult Reset() { Resets++; return TimerControlResult.Applied(); }
        }
    }
}
