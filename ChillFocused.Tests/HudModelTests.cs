using System;
using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The overlay's pure line builder: UI can be exercised with a literal model,
    /// without Unity, a game or a backend.
    /// </summary>
    public class HudModelTests
    {
        private static TextLookup Lookup(IDictionary<string, string> calls)
        {
            return delegate(string key, string chinese)
            {
                calls[key] = chinese;
                return chinese;
            };
        }

        private static HudModel Model(HudMode mode = HudMode.Minimal)
        {
            return new HudModel
            {
                Mode = mode,
                Text = Lookup(new Dictionary<string, string>()),
                Connected = true,
                Enforcing = true,
                GateSatisfied = true,
                Version = "0.2.0",
                FrozenCount = 1,
                FrozenTotal = 5,
                RuleNameCount = 2,
                FrozenNames = new[] { "firefox" },
                RecentNames = new[] { "discord" },
                TimerKnown = true,
                TimerWorking = true,
                ToggleHudKey = "F9",
                ToggleSettingsKey = "F7",
            };
        }

        [Fact]
        public void Minimal_shows_the_current_count_not_the_lifetime_total()
        {
            var lines = new List<string>();

            HudState.BuildLines(lines, Model());

            Assert.Single(lines);
            Assert.Contains("已冻结 1", lines[0]);
            Assert.DoesNotContain("5", lines[0]);
        }

        [Fact]
        public void Detailed_separates_current_and_total()
        {
            var lines = new List<string>();

            HudState.BuildLines(lines, Model(HudMode.Detailed));

            Assert.Contains(lines, line => line.Contains("当前冻结  ·  1"));
            Assert.Contains(lines, line => line.Contains("累计处理  ·  5"));
            Assert.Contains(lines, line => line.Contains("已连接 v0.2.0"));
        }

        [Fact]
        public void Degraded_and_warnings_are_marked_for_the_renderer()
        {
            var model = Model(HudMode.Detailed);
            model.Degraded = true;
            model.LastWarning = "boom";
            var lines = new List<string>();

            HudState.BuildLines(lines, model);

            Assert.Contains(lines, line => line.StartsWith("!", StringComparison.Ordinal) && line.Contains("后端扫描异常"));
            Assert.Contains(lines, line => line.Contains("最近错误"));
        }

        [Fact]
        public void Off_renders_no_lines()
        {
            var lines = new List<string> { "stale" };

            HudState.BuildLines(lines, Model(HudMode.Off));

            Assert.Empty(lines);
        }
    }
}
