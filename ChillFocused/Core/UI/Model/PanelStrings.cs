using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Wording for the panels: the user's text table when it has the key, otherwise the
    /// built-in wording.
    /// </summary>
    /// <remarks>
    /// Split out of the settings window so both halves of it -- and the picker next to
    /// it -- resolve text the same way. Nothing but the key and the built-in wording
    /// arrives, which keeps this file free of BepInEx and lets the fallback and the
    /// placeholder handling be unit tested.
    /// </remarks>
    public static class PanelStrings
    {
        /// <summary>The text overrides, supplied by the runner; null means built-in only.</summary>
        public static PanelText Text;

        /// <summary>Configured wording when given, otherwise the built-in one.</summary>
        public static string S(string key, string chinese)
        {
            return Text != null ? Text.Get(key, chinese) : chinese;
        }

        /// <summary>Like S, but substitutes {0}, {1} ... into the wording.</summary>
        public static string Sf(string key, string chinese, params object[] args)
        {
            var format = S(key, chinese);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                // A hand-edited file can easily lose a placeholder; showing the raw
                // string beats throwing inside OnGUI.
                return format;
            }
        }

        /// <summary>
        /// The verb for a row action, with the affected name appended when there is one.
        /// </summary>
        public static string Notice(string key, string chinese, string name)
        {
            var verb = S(key, chinese);
            return string.IsNullOrEmpty(name) ? verb : verb + " " + name;
        }

        /// <summary>A configured string when it has something in it, otherwise the built-in one.</summary>
        public static string Override(string custom, string fallback)
        {
            return string.IsNullOrEmpty(custom) ? fallback : custom;
        }
    }
}
