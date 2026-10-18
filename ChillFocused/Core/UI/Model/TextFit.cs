using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Fitting text into a known width, without depending on Unity.
    /// </summary>
    /// <remarks>
    /// Taking the measurement as a delegate is what makes this testable: in the game it
    /// is <c>GUIStyle.CalcSize</c>, and in a unit test it is anything, including a fake
    /// where one character costs a fixed number of pixels.
    /// </remarks>
    public static class TextFit
    {
        /// <summary>The character appended to show that text was removed.</summary>
        public const string Ellipsis = "\u2026";

        /// <summary>Trim to a character count, appending an ellipsis when shortened.</summary>
        public static string Shorten(string text, int limit)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            // No room at all means no text. Returning the full string here would hand the
            // caller something it explicitly said it had no space for.
            if (limit <= 0)
            {
                return string.Empty;
            }

            if (text.Length <= limit)
            {
                return text;
            }

            if (limit == 1)
            {
                return Ellipsis;
            }

            return text.Substring(0, limit - 1).TrimEnd() + Ellipsis;
        }

        /// <summary>
        /// Trim until it measures within <paramref name="maxWidth"/>.
        /// </summary>
        /// <returns>
        /// The original text when it already fits or cannot be measured; otherwise a
        /// prefix with an ellipsis. A width that cannot fit even the ellipsis yields the
        /// ellipsis alone, because returning nothing would look like a missing row.
        /// </returns>
        public static string Fit(string text, float maxWidth, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text) || measure == null || maxWidth <= 0f)
            {
                return text ?? string.Empty;
            }

            float width;
            try
            {
                width = measure(text);
            }
            catch (Exception)
            {
                return text;
            }

            if (width <= maxWidth)
            {
                return text;
            }

            // Walk inwards from an upper bound. Binary search would be fewer calls, but
            // these strings are short and the measurement is not free, so a plain scan
            // costs little and cannot land on an awkward off-by-one.
            for (var length = text.Length - 1; length >= 1; length--)
            {
                var candidate = text.Substring(0, length).TrimEnd() + Ellipsis;
                try
                {
                    if (measure(candidate) <= maxWidth)
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    return text;
                }
            }

            return Ellipsis;
        }
    }
}
