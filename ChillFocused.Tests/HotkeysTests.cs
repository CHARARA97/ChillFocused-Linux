using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class HotkeysTests
    {
        private static TextLookup Capture(IDictionary<string, string> calls)
        {
            return delegate(string key, string chinese)
            {
                calls[key] = chinese;
                return "[" + key + "]";
            };
        }

        [Fact]
        public void Edge_fires_on_the_frame_the_key_goes_down()
        {
            var previous = false;

            Assert.True(Hotkeys.Edge(true, ref previous));
            Assert.True(previous);
        }

        [Fact]
        public void Edge_does_not_fire_again_while_the_key_is_held()
        {
            var previous = false;

            Hotkeys.Edge(true, ref previous);

            Assert.False(Hotkeys.Edge(true, ref previous));
            Assert.False(Hotkeys.Edge(true, ref previous));
        }

        [Fact]
        public void Edge_fires_again_after_the_key_is_released()
        {
            var previous = false;

            Hotkeys.Edge(true, ref previous);
            Assert.False(Hotkeys.Edge(false, ref previous));
            Assert.False(previous);

            Assert.True(Hotkeys.Edge(true, ref previous));
        }

        [Fact]
        public void Edge_stays_quiet_for_a_key_that_is_never_held()
        {
            var previous = false;

            for (var i = 0; i < 5; i++)
            {
                Assert.False(Hotkeys.Edge(false, ref previous));
            }
        }

        [Fact]
        public void The_overlay_key_cycles_through_its_three_sizes()
        {
            Assert.Equal(HudMode.Detailed, Hotkeys.NextHudMode(HudMode.Minimal));
            Assert.Equal(HudMode.Off, Hotkeys.NextHudMode(HudMode.Detailed));
            Assert.Equal(HudMode.Minimal, Hotkeys.NextHudMode(HudMode.Off));
        }

        [Fact]
        public void Three_presses_return_the_overlay_to_where_it_started()
        {
            var mode = HudMode.Minimal;

            for (var i = 0; i < 3; i++)
            {
                mode = Hotkeys.NextHudMode(mode);
            }

            Assert.Equal(HudMode.Minimal, mode);
        }

        [Fact]
        public void HudModeNotice_names_the_size_the_overlay_switched_to()
        {
            var calls = new Dictionary<string, string>();

            Assert.Equal("[overlay.overlay_one_line]",
                         Hotkeys.HudModeNotice(HudMode.Minimal, Capture(calls)));
            Assert.Equal("[overlay.overlay_detailed]",
                         Hotkeys.HudModeNotice(HudMode.Detailed, Capture(calls)));
            Assert.Equal("面板：一行", calls["overlay.overlay_one_line"]);
            Assert.Equal("面板：详细", calls["overlay.overlay_detailed"]);
        }

        [Fact]
        public void HudModeNotice_says_nothing_when_the_overlay_goes_hidden()
        {
            Assert.Null(Hotkeys.HudModeNotice(HudMode.Off, Capture(new Dictionary<string, string>())));
        }

        [Fact]
        public void HudModeNotice_falls_back_to_the_built_in_wording()
        {
            Assert.Equal("面板：一行", Hotkeys.HudModeNotice(HudMode.Minimal, null));
            Assert.Equal("面板：详细", Hotkeys.HudModeNotice(HudMode.Detailed, null));
        }
    }
}
