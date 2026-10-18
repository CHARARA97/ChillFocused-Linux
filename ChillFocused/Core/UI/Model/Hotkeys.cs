namespace ChillFocused.Core
{
    /// <summary>
    /// The keyboard-facing rules: when a held key counts as a press, what the overlay
    /// key cycles through, and what the player is told about it.
    /// </summary>
    /// <remarks>
    /// Extracted from the runner because all three are plain decisions over a bool, an
    /// enum and a text lookup. Reading the key itself stays with the runner, which owns
    /// the BepInEx bindings and a per-frame clock; that also leaves the edge detection
    /// testable on its own.
    /// </remarks>
    public static class Hotkeys
    {
        /// <summary>
        /// True only on the frame <paramref name="down"/> turns true, so holding a key
        /// does not repeat its action.
        /// </summary>
        public static bool Edge(bool down, ref bool previous)
        {
            var pressed = down && !previous;
            previous = down;
            return pressed;
        }

        /// <summary>F9 walks the overlay through its three sizes.</summary>
        public static HudMode NextHudMode(HudMode current)
        {
            if (current == HudMode.Minimal)
            {
                return HudMode.Detailed;
            }

            return current == HudMode.Detailed ? HudMode.Off : HudMode.Minimal;
        }

        /// <summary>
        /// What to say when the overlay changes size, or <c>null</c> for the off mode:
        /// there is nothing to see once the overlay is hidden, so there is nothing to say.
        /// </summary>
        public static string HudModeNotice(HudMode mode, TextLookup text)
        {
            if (mode == HudMode.Off)
            {
                return null;
            }

            return mode == HudMode.Minimal
                ? HudState.Resolve(text, "overlay.overlay_one_line", "面板：一行")
                : HudState.Resolve(text, "overlay.overlay_detailed", "面板：详细");
        }
    }
}
