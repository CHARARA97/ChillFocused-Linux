using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>
    /// Reading, comparing and extending an INI-style config file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The commented template is the single source of truth: it already lists every key
    /// with its default value and groups them under <c># --- name ---</c> banners. So
    /// "which keys are missing, and where do they belong" is derivable from it, and there
    /// is no second list to keep in step.
    /// </para>
    /// <para>
    /// Nothing here writes: the caller decides whether to add keys. A mod that rewrites a
    /// player's config on startup can silently destroy their edits, which is exactly what
    /// happened during development of this one.
    /// </para>
    /// </remarks>
    public static class ConfigFile
    {
        /// <summary>One template entry: a key, its default, and the section it sits in.</summary>
        public struct Entry
        {
            public string Key;
            public string Value;
            public string Section;

            public override string ToString()
            {
                return Key + " = " + Value;
            }
        }

        /// <summary>Parse a commented template into its entries, in file order.</summary>
        public static List<Entry> Template(string template)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(template))
            {
                return entries;
            }

            var section = string.Empty;
            var lines = template.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '#' || line[0] == ';')
                {
                    var banner = SectionName(line);
                    if (banner != null)
                    {
                        section = banner;
                    }

                    continue;
                }

                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                entries.Add(new Entry
                {
                    Key = line.Substring(0, split).Trim(),
                    Value = line.Substring(split + 1).Trim(),
                    Section = section,
                });
            }

            return entries;
        }

        /// <summary>The section name in a banner comment, or null for an ordinary comment.</summary>
        public static string SectionName(string comment)
        {
            // "# --- 位置 ---" and "# --- 位置 ---------" both name the section 位置.
            var text = (comment ?? string.Empty).TrimStart('#', ';', ' ', '\t');
            if (!text.StartsWith("---", StringComparison.Ordinal))
            {
                return null;
            }

            text = text.Substring(3).TrimStart('-', ' ');
            var end = text.IndexOf("---", StringComparison.Ordinal);
            if (end >= 0)
            {
                text = text.Substring(0, end);
            }

            text = text.TrimEnd('-', ' ');
            return text.Length == 0 ? null : text;
        }

        /// <summary>The keys present in a real config file.</summary>
        public static HashSet<string> Keys(string text)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text))
            {
                return keys;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                var split = line.IndexOf('=');
                if (split > 0)
                {
                    keys.Add(line.Substring(0, split).Trim());
                }
            }

            return keys;
        }

        /// <summary>Template entries the file does not mention, in file order.</summary>
        public static List<Entry> Missing(string text, string template)
        {
            var present = Keys(text);
            var missing = new List<Entry>();
            foreach (var entry in Template(template))
            {
                if (!present.Contains(entry.Key))
                {
                    missing.Add(entry);
                }
            }

            return missing;
        }

        /// <summary>
        /// The file with every missing key inserted at the end of its own section.
        /// </summary>
        /// <remarks>
        /// Existing lines are never rewritten and never reordered, so a hand-edited file
        /// keeps its formatting and its values; new keys simply appear next to their
        /// neighbours instead of accumulating at the bottom of the file.
        /// </remarks>
        public static string Insert(string text, string template)
        {
            var missing = Missing(text, template);
            if (missing.Count == 0)
            {
                return text ?? string.Empty;
            }

            var bySection = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var entry in missing)
            {
                List<Entry> bucket;
                if (!bySection.TryGetValue(entry.Section, out bucket))
                {
                    bucket = new List<Entry>();
                    bySection[entry.Section] = bucket;
                    order.Add(entry.Section);
                }

                bucket.Add(entry);
            }

            var lines = new List<string>((text ?? string.Empty).Replace("\r\n", "\n").Split('\n'));
            foreach (var section in order)
            {
                var insertAt = lines.Count;
                if (section.Length > 0)
                {
                    for (var i = lines.Count - 1; i >= 0; i--)
                    {
                        var banner = SectionName(lines[i].Trim());
                        if (banner == section)
                        {
                            insertAt = i + 1;
                            break;
                        }
                    }
                }

                // Skip the comment lines that introduce the section, so the new key lands
                // after the explanation rather than between it and the entries.
                while (insertAt < lines.Count && lines[insertAt].TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    insertAt++;
                }

                var block = new List<string>();
                foreach (var entry in bySection[section])
                {
                    block.Add(entry.ToString());
                }

                lines.InsertRange(insertAt, block);
            }

            return string.Join("\n", lines.ToArray());
        }
    }
}
