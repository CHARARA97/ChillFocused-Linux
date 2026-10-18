using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class SettingsRefresherTests
    {
        [Fact]
        public void Refreshes_once_per_second_and_reports_rule_changes()
        {
            var current = new FocusSettings { ProcessNames = new[] { "a" } };
            var next = new FocusSettings { ProcessNames = new[] { "b" } };
            var provider = next;
            var refresher = new SettingsRefresher(() => provider);

            Assert.True(refresher.TryRefresh(0f, ref current));
            Assert.Equal("b", current.ProcessNames[0]);

            provider = new FocusSettings { ProcessNames = new[] { "c" } };
            Assert.False(refresher.TryRefresh(0.5f, ref current));
            Assert.True(refresher.TryRefresh(1f, ref current));
            Assert.Equal("c", current.ProcessNames[0]);
        }

        [Fact]
        public void Hud_only_changes_update_the_existing_object()
        {
            var current = new FocusSettings { Hud = HudMode.Minimal };
            var provider = new FocusSettings { Hud = HudMode.Detailed };
            var refresher = new SettingsRefresher(() => provider);

            Assert.True(refresher.TryRefresh(0f, ref current));
            Assert.Equal(HudMode.Detailed, current.Hud);
        }
    }
}
