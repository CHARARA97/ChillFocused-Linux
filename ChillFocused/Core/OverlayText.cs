using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>
    /// The overlay's line encoding, and small text helpers.
    /// </summary>
    /// <remarks>
    /// A HUD line carries a one-character marker at the front: <c>!</c> means "draw this
    /// as a warning". Keeping the marker in the string is what makes the draw path a
    /// two-line lookup, but it also means every reader has to strip it, so the parsing
    /// lives here rather than being repeated. Extracted from the runner because it is
    /// pure string handling and can therefore be tested without Unity.
    /// </remarks>
    public static class OverlayText
    {
        /// <summary>Prefix marking a line as a warning.</summary>
        public const string WarningMarker = "!";

        /// <summary>The line's text, without its marker.</summary>
        public static string TextFor(string line)
        {
            return line != null && line.StartsWith(WarningMarker, StringComparison.Ordinal)
                ? line.Substring(1)
                : line ?? string.Empty;
        }

        /// <summary>Append a warning line.</summary>
        public static void AddWarn(List<string> lines, string text)
        {
            if (lines != null)
            {
                lines.Add(WarningMarker + (text ?? string.Empty));
            }
        }

        /// <summary>Trim to a character count, appending an ellipsis when shortened.</summary>
        public static string Clip(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || max <= 0)
            {
                return string.Empty;
            }

            return value.Length <= max ? value : value.Substring(0, max - 1) + "\u2026";
        }
    }
}
