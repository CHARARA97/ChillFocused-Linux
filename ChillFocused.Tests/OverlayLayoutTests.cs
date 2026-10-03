using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class OverlayLayoutTests
    {
        // A 1600x1000 screen with a 400x120 panel, 10px margins.
        private static OverlayAnchor Anchor(string corner)
        {
            return OverlayLayout.AnchorFor(corner, 400f, 120f, 1600f, 1000f, 10f, 10f);
        }

        [Fact]
        public void Top_left_uses_both_margins()
        {
            var anchor = Anchor("top-left");

            Assert.Equal(10f, anchor.x);
            Assert.Equal(10f, anchor.y);
        }

        [Fact]
        public void Right_hand_corners_subtract_the_panel_width()
        {
            var anchor = Anchor("top-right");

            Assert.Equal(1600f - 400f - 10f, anchor.x);
            Assert.Equal(10f, anchor.y);
        }

        [Fact]
        public void Bottom_corners_subtract_the_panel_height()
        {
            var anchor = Anchor("bottom-left");

            Assert.Equal(10f, anchor.x);
            Assert.Equal(1000f - 120f - 10f, anchor.y);
        }

        [Fact]
        public void Bottom_right_subtracts_both()
        {
            var anchor = Anchor("bottom-right");

            Assert.Equal(1190f, anchor.x);
            Assert.Equal(870f, anchor.y);
        }

        [Fact]
        public void An_unknown_corner_falls_back_to_the_top_left()
        {
            Assert.Equal(10f, Anchor("middle").x);
            Assert.Equal(10f, Anchor("middle").y);
            Assert.Equal(10f, Anchor(null).x);
            Assert.Equal(10f, Anchor(null).y);
        }

        [Fact]
        public void The_corner_name_is_trimmed_and_case_insensitive()
        {
            var anchor = Anchor("  Bottom-Right  ");

            Assert.Equal(1190f, anchor.x);
            Assert.Equal(870f, anchor.y);
        }

        [Fact]
        public void A_partial_corner_name_still_picks_the_edge_it_names()
        {
            // Matches the previous behaviour: "right" places right and top.
            Assert.Equal(1190f, Anchor("right").x);
            Assert.Equal(10f, Anchor("right").y);
            Assert.Equal(10f, Anchor("bottom").x);
            Assert.Equal(870f, Anchor("bottom").y);
        }
    }
}
