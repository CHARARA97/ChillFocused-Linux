using System.Text;

namespace ChillFocused.Core
{
    /// <summary>Serialises a rule set to Focused's wire format by hand.</summary>
    internal static class RuleJson
    {
        public static string Build(FocusSettings settings)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"names\":");
            AppendArray(builder, settings.ProcessNames);
            builder.Append(",\"cmdline_substrings\":");
            AppendArray(builder, settings.CmdlineSubstrings);
            builder.Append(",\"pids\":[],\"pid_guards\":{},\"protect_names\":");
            AppendArray(builder, settings.ProtectedNames);
            builder.Append(",\"protect_cmdline_substrings\":");
            AppendArray(builder, settings.ProtectedCmdlineSubstrings);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendArray(StringBuilder builder, string[] values)
        {
            builder.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendString(builder, values[i]);
                }
            }

            builder.Append(']');
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (ch < 0x20)
                        {
                            builder.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
