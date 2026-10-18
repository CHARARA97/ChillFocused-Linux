using System;
using System.Collections.Generic;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// Looks for a second copy of this plugin inside BepInEx's plugin folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BepInEx loads every DLL it finds under <c>BepInEx/plugins</c>, subdirectories
    /// included.  So an upgrade that leaves the old file behind -- a different
    /// folder, the pre-rename name, a leftover <c>-old</c> copy -- makes the game
    /// run the plugin <b>twice</b>: two overlays, two poll loops, two sets of config
    /// writes.  Nothing errors; it just behaves strangely.
    /// </para>
    /// <para>
    /// Unity-free and free of filesystem access on purpose: the caller enumerates
    /// the directory, this decides what the listing means, and the tests feed it
    /// synthetic lists.
    /// </para>
    /// </remarks>
    internal static class PluginInstallCheck
    {
        /// <summary>
        /// File-name stems that belong to this plugin, current and retired.  The
        /// renamed one matters: <c>ChillFocus.dll</c> from before the merge is a real
        /// upgrade path, not a hypothetical.
        /// </summary>
        private static readonly string[] OwnStems = { "ChillFocused", "ChillFocus" };

        /// <summary>
        /// How much of a name counts as "the same plugin": the stem exactly, or the
        /// stem followed by a separator (<c>ChillFocused-old</c>, <c>ChillFocus v2</c>).
        /// A prefix alone would flag unrelated files such as <c>ChillFocusable.dll</c>.
        /// </summary>
        private static bool LooksLikeThisPlugin(string path)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(stem))
            {
                return false;
            }

            for (var i = 0; i < OwnStems.Length; i++)
            {
                var own = OwnStems[i];
                if (string.Equals(stem, own, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (stem.Length > own.Length
                    && string.Equals(stem.Substring(0, own.Length), own, StringComparison.OrdinalIgnoreCase))
                {
                    var next = stem[own.Length];
                    if (next == '-' || next == '_' || next == '.' || next == ' ')
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The DLLs under the plugin folder that look like another copy of this
        /// plugin.  <paramref name="selfPath"/> is excluded by exact path (not
        /// case-insensitively: on Linux <c>ChillFocused.dll</c> and
        /// <c>chillfocused.dll</c> are genuinely two files, and BepInEx loads both).
        /// </summary>
        public static string[] ForeignCopies(IEnumerable<string> pluginDlls, string selfPath)
        {
            var found = new List<string>();
            if (pluginDlls == null)
            {
                return found.ToArray();
            }

            foreach (var path in pluginDlls)
            {
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(selfPath)
                    && string.Equals(Path.GetFullPath(path), Path.GetFullPath(selfPath), StringComparison.Ordinal))
                {
                    continue;
                }

                if (LooksLikeThisPlugin(path))
                {
                    found.Add(path);
                }
            }

            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        /// <summary>
        /// A warning worth putting in the log, or <c>null</c> when the install is
        /// clean.  Kept short: this ends up in LogOutput.log next to everything else.
        /// </summary>
        public static string Warning(string[] foreignCopies, string selfPath)
        {
            if (foreignCopies == null || foreignCopies.Length == 0)
            {
                return null;
            }

            var lines = new List<string>();
            lines.Add("another copy of this plugin is installed; BepInEx loads every DLL under plugins/,");
            lines.Add("so the mod would run twice.  Loaded from: " + (selfPath ?? "?"));
            for (var i = 0; i < foreignCopies.Length; i++)
            {
                lines.Add("  remove: " + foreignCopies[i]);
            }

            return string.Join("\n", lines.ToArray());
        }
    }
}
