using System;

namespace ChillFocused.Core
{
    /// <summary>The small part of GET /focus the sync loop needs.</summary>
    public struct FocusStatusProbeResult
    {
        public string Raw;
        public bool? Active;
        public int? Pid;
    }

    /// <summary>
    /// Reads Focused's flat status and extracts the few fields the sync thread uses.
    /// </summary>
    /// <remarks>
    /// The request itself is a delegate so the parsing can be tested without a
    /// socket, and so the sync runner keeps ownership of the transport.
    /// </remarks>
    public sealed class FocusStatusProbe
    {
        private readonly Func<string> _read;

        public FocusStatusProbe(Func<string> read)
        {
            _read = read ?? (() => null);
        }

        public FocusStatusProbeResult Read()
        {
            var raw = _read();
            return new FocusStatusProbeResult
            {
                Raw = raw,
                Active = ReadBool(raw, "active"),
                Pid = ReadInt(raw, "pid"),
            };
        }

        /// <summary>
        /// Pull a boolean out of a JSON response without a JSON parser.
        /// A miss means "unknown", never "false".
        /// </summary>
        public static bool? ReadBool(string json, string key)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var needle = "\"" + key + "\"";
            var index = json.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                var after = index + needle.Length;
                while (after < json.Length && (json[after] == ' ' || json[after] == ':'))
                {
                    after++;
                }

                if (after < json.Length)
                {
                    if (string.CompareOrdinal(json, after, "true", 0, 4) == 0)
                    {
                        return true;
                    }

                    if (string.CompareOrdinal(json, after, "false", 0, 5) == 0)
                    {
                        return false;
                    }
                }

                index = json.IndexOf(needle, index + 1, StringComparison.Ordinal);
            }

            return null;
        }

        /// <summary>Pull an integer out of a response; used for Focused's pid.</summary>
        public static int? ReadInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var needle = "\"" + key + "\"";
            var index = json.IndexOf(needle, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var after = index + needle.Length;
            while (after < json.Length && (json[after] == ' ' || json[after] == ':'))
            {
                after++;
            }

            var start = after;
            while (after < json.Length && json[after] >= '0' && json[after] <= '9')
            {
                after++;
            }

            int value;
            return after > start && int.TryParse(json.Substring(start, after - start), out value)
                ? (int?)value
                : null;
        }
    }
}
