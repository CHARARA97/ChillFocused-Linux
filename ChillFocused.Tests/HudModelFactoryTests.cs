using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class HudModelFactoryTests
    {
        [Fact]
        public void Builds_the_projection_from_status_and_settings()
        {
            var settings = new FocusSettings { Hud = HudMode.Detailed, ProcessNames = new[] { "a", "b" } };
            var status = FocusStatusMapper.Apply(new FocusStatus(), new FocusStateResponse
            {
                ok = true,
                active = true,
                frozen_count = 2,
                frozen_total = 5,
                frozen_names = new[] { "firefox" },
                recent_names = new[] { "discord" },
                version = "0.2.0",
            });
            status = status.WithGate(true, true);

            var model = HudModelFactory.Build(settings, status, "F7", "F8", (key, chinese) => chinese);

            Assert.Equal(HudMode.Detailed, model.Mode);
            Assert.True(model.Connected);
            Assert.True(model.Enforcing);
            Assert.True(model.GateSatisfied);
            Assert.Equal(2, model.FrozenCount);
            Assert.Equal(2, model.RuleNameCount);
            Assert.Equal("F7", model.ToggleHudKey);
        }
    }
}
