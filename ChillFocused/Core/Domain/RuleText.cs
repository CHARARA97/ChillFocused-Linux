using System;
using System.Collections.Generic;
using System.Text;

namespace ChillFocused.Core
{
    /// <summary>
    /// String and glob helpers that mirror Focused's rule semantics.
    /// </summary>
    /// <remarks>
    /// This file must never reference UnityEngine. It is compiled both into the
    /// plugin and into ChillFocused.Tests, and that test project is what keeps the
    /// client-side interpretation of a rule identical to Focused's.
    /// </remarks>
    public static class RuleText
    {
        /// <summary>Linux caps a task name at 15 visible characters.</summary>
        public const int CommMaxLength = 15;

        /// <summary>
        /// Separators accepted in the BepInEx configuration strings.
        /// Comma is deliberately absent: command-line substrings legitimately
        /// contain commas (for example <c>--flag=a,b</c>).
        /// </summary>
        public static readonly char[] Separators = { ';', '\n', '\r' };

        /// <summary>Split a config string into trimmed, de-duplicated entries.</summary>
        public static List<string> Split(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = piece.Trim();
                if (trimmed.Length == 0 || !seen.Add(trimmed))
                {
                    continue;
                }

                result.Add(trimmed);
            }

            return result;
        }

        public static string[] ToArray(string raw)
        {
            return Split(raw).ToArray();
        }

        public static bool HasWildcard(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '*' || pattern[i] == '?')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True for patterns consisting only of glob syntax (<c>*</c>, <c>?</c>).
        /// Such a pattern would match every process on the system; Focused
        /// rejects it, and the plugin warns about it before the push.
        /// </summary>
        public static bool IsCatchAllPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            for (var i = 0; i < pattern.Length; i++)
            {
                var ch = pattern[i];
                if (ch != '*' && ch != '?')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Case-insensitive <c>*</c>/<c>?</c> glob match (no character classes).
        /// Iterative with backtracking, so malformed patterns cannot blow the
        /// stack.
        /// </summary>
        public static bool GlobMatches(string pattern, string text)
        {
            if (pattern == null || text == null)
            {
                return false;
            }

            int p = 0, t = 0, star = -1, mark = 0;
            while (t < text.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]))
                {
                    p++;
                    t++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p++;
                    mark = t;
                }
                else if (star >= 0)
                {
                    p = star + 1;
                    t = ++mark;
                }
                else
                {
                    return false;
                }
            }

            while (p < pattern.Length && pattern[p] == '*')
            {
                p++;
            }

            return p == pattern.Length;
        }

        /// <summary>
        /// Does <paramref name="pattern"/> match the process name
        /// <paramref name="candidate"/>?
        /// </summary>
        /// <remarks>
        /// Mirrors Focused, including its compensation for the kernel's
        /// 15-character truncation of <c>/proc/&lt;pid&gt;/comm</c>: a plain rule
        /// longer than 15 characters must still match a truncated 15-character
        /// name it starts with. The compensation is skipped for glob patterns,
        /// because truncation and wildcards cannot both be reasoned about
        /// precisely.
        /// </remarks>
        public static bool NameMatches(string pattern, string candidate)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            var p = pattern.Trim().ToLowerInvariant();
            var c = candidate.Trim().ToLowerInvariant();
            if (GlobMatches(p, c))
            {
                return true;
            }

            if (!HasWildcard(p)
                && c.Length == CommMaxLength
                && p.Length > CommMaxLength
                && p.StartsWith(c, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        /// <summary>Human-readable, length-capped rendering of a rule list.</summary>
        public static string Describe(IEnumerable<string> values, int max)
        {
            if (values == null)
            {
                return "(none)";
            }

            var list = new List<string>();
            foreach (var value in values)
            {
                list.Add(value);
            }

            if (list.Count == 0)
            {
                return "(none)";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < list.Count; i++)
            {
                if (i >= max)
                {
                    builder.Append(", +").Append(list.Count - max).Append(" more");
                    break;
                }

                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(list[i]);
            }

            return builder.ToString();
        }
    }
}
