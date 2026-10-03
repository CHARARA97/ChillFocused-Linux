using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class HudStateTests
    {
        /// <summary>A lookup that answers with a marker and records the Chinese wording.</summary>
        private static TextLookup Capture(IDictionary<string, string> calls)
        {
            return delegate(string key, string chinese)
            {
                calls[key] = chinese;
                return "[" + key + "]";
            };
        }

        [Fact]
        public void StateReason_reports_a_missing_executor_first()
        {
            var calls = new Dictionary<string, string>();

            var reason = HudState.StateReason(Capture(calls), false, true, true);

            Assert.Equal("[overlay.not_enforcing_executor_not_connected]", reason);
            Assert.Single(calls);
        }

        [Fact]
        public void StateReason_names_the_work_session_when_the_gate_is_satisfied()
        {
            var calls = new Dictionary<string, string>();

            var reason = HudState.StateReason(Capture(calls), true, true, true);

            Assert.Equal("[overlay.enforcing_work_session]", reason);
            Assert.Equal("生效中 · 创作模式", calls["overlay.enforcing_work_session"]);
        }

        [Fact]
        public void StateReason_distinguishes_enforcing_without_a_satisfied_gate()
        {
            var calls = new Dictionary<string, string>();

            var reason = HudState.StateReason(Capture(calls), true, true, false);

            Assert.Equal("[overlay.enforcing]", reason);
        }

        [Fact]
        public void StateReason_points_at_the_game_timer_when_nothing_enforces()
        {
            var calls = new Dictionary<string, string>();

            var reason = HudState.StateReason(Capture(calls), true, false, false);

            Assert.Equal("[overlay.not_enforcing_start_the_game_s_timer]", reason);
        }

        [Fact]
        public void StateReason_falls_back_to_the_built_in_wording()
        {
            Assert.Equal("生效中 · 创作模式",
                         HudState.StateReason(null, true, true, true));
            Assert.Equal("未生效 · 后台执行器未连接",
                         HudState.StateReason(null, false, false, false));
        }

        [Fact]
        public void RecentNames_joins_the_most_recent_names()
        {
            var recent = new[] { "a.exe", "b.exe", "c.exe", "d.exe" };

            Assert.Equal("a.exe · b.exe · c.exe", HudState.RecentNames(recent, 3));
        }

        [Fact]
        public void RecentNames_returns_everything_when_there_is_less_than_asked_for()
        {
            var recent = new[] { "only.exe" };

            Assert.Equal("only.exe", HudState.RecentNames(recent, 3));
        }

        [Fact]
        public void RecentNames_skips_nameless_entries_without_counting_them()
        {
            var recent = new[] { null, string.Empty, "a.exe", null, "b.exe" };

            Assert.Equal("a.exe · b.exe", HudState.RecentNames(recent, 2));
        }

        [Fact]
        public void RecentNames_is_empty_without_entries_or_a_requested_count()
        {
            Assert.Equal(string.Empty, HudState.RecentNames(null, 3));
            Assert.Equal(string.Empty, HudState.RecentNames(new string[0], 3));
            Assert.Equal(string.Empty, HudState.RecentNames(new[] { "a.exe" }, 0));
            Assert.Equal(string.Empty, HudState.RecentNames(new[] { "a.exe" }, -1));
        }

        [Fact]
        public void PanelHeadline_reports_a_missing_executor_first()
        {
            var calls = new Dictionary<string, string>();

            var headline = HudState.PanelHeadline(Capture(calls), false, true, true, true);

            Assert.Equal("\u25cb [panel.not_active_executor_offline]", headline);
            Assert.Single(calls);
        }

        [Fact]
        public void PanelHeadline_shows_always_on_when_the_gate_is_switched_off()
        {
            var calls = new Dictionary<string, string>();

            var headline = HudState.PanelHeadline(Capture(calls), true, true, false, false);

            Assert.Equal("\u25cf [panel.active_always_on]", headline);
            Assert.Equal("生效中 · 不依赖计时器", calls["panel.active_always_on"]);
        }

        [Fact]
        public void PanelHeadline_shows_the_work_session()
        {
            var calls = new Dictionary<string, string>();

            var headline = HudState.PanelHeadline(Capture(calls), true, true, true, true);

            Assert.Equal("\u25cf [panel.active_work_session_running]", headline);
            Assert.Equal("生效中 · 创作模式", calls["panel.active_work_session_running"]);
        }

        [Fact]
        public void PanelHeadline_waits_when_the_gate_is_not_satisfied()
        {
            var calls = new Dictionary<string, string>();

            var headline = HudState.PanelHeadline(Capture(calls), true, true, true, false);

            Assert.Equal("\u25cb [panel.waiting_start_the_game_s_timer]", headline);
        }

        [Fact]
        public void PanelHeadline_waits_while_the_gate_is_unknown()
        {
            var calls = new Dictionary<string, string>();

            var headline = HudState.PanelHeadline(Capture(calls), true, false, false, false);

            Assert.Equal("\u25cb [panel.waiting_start_the_game_s_timer]", headline);
        }

        [Fact]
        public void PanelHeadline_falls_back_to_the_built_in_wording()
        {
            Assert.Equal("\u25cb 未生效 · 后台执行器未连接",
                         HudState.PanelHeadline(null, false, false, false, false));
            // The gate-off headline used to say "始终拦截"; the interface is
            // freeze-only now, so it describes the timing instead of the action.
            Assert.Equal("\u25cf 生效中 · 不依赖计时器",
                         HudState.PanelHeadline(null, true, true, false, false));
        }
    }
}
