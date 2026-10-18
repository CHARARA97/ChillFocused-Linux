using System;

namespace ChillFocused.Core
{
    /// <summary>Where the overlay sits, in screen pixels.</summary>
    public struct OverlayAnchor
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// The overlay's screen placement.
    /// </summary>
    /// <remarks>
    /// Split out of the renderer because the corner arithmetic is easy to get wrong --
    /// a right-hand corner needs the panel's width, a bottom corner its height, and the
    /// measurement from last frame is what supplies both. Keeping it here means every
    /// corner can be checked without a running game.
    /// </remarks>
    public static class OverlayLayout
    {
        /// <summary>
        /// Top-left corner of the panel for a corner name such as <c>top-right</c> or
        /// <c>bottom-left</c>. Unknown names fall back to the top-left.
        /// </summary>
        public static OverlayAnchor AnchorFor(string corner, float width, float height,
                                             float screenWidth, float screenHeight,
                                             float marginX, float marginY)
        {
            var name = (corner ?? string.Empty).Trim().ToLowerInvariant();

            var x = name.EndsWith("right", StringComparison.Ordinal)
                ? screenWidth - width - marginX
                : marginX;
            var y = name.StartsWith("bottom", StringComparison.Ordinal)
                ? screenHeight - height - marginY
                : marginY;

            return new OverlayAnchor { x = x, y = y };
        }
    }
}
