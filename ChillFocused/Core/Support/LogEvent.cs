using System.Text;

namespace ChillFocused.Core
{
    /// <summary>
    /// Small formatter for greppable plugin log lines.
    /// </summary>
    /// <remarks>
    /// Legacy log calls are prefixed with [ChillFocused]; new operational events use
    /// event=plugin.&lt;name&gt;. Both can be filtered with one string, and reasons are
    /// kept on the same line so a LogOutput.log snippet is enough to diagnose a run.
    /// </remarks>
    internal static class LogEvent
    {
        internal static string Line(string name, params string[] fields)
        {
            var b = new StringBuilder("event=plugin.");
            b.Append(name ?? "unknown");
            if (fields != null)
            {
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = OneLine(fields[i]);
                    if (field.Length == 0)
                    {
                        continue;
                    }

                    b.Append(' ').Append(field);
                }
            }

            return b.ToString();
        }

        internal static string Prefixed(string message)
        {
            var text = OneLine(message);
            if (text.Length == 0)
            {
                return "[ChillFocused]";
            }

            return text.StartsWith("[ChillFocused]")
                ? text
                : "[ChillFocused] " + text;
        }

        private static string OneLine(string value)
        {
            return (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }
    }
}
