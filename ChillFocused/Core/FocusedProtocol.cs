using System;

namespace ChillFocused.Core
{
    // Wire format for Focused's JSON API.
    //
    // These types are shaped for Unity's JsonUtility:
    //   * [Serializable] classes with public fields (no properties);
    //   * arrays always wrapped in an object, never top level;
    //   * no dictionaries -- "pid_guards" is simply left out, because the
    //     plugin only ever sends name and cmdline rules.
    //
    // Field names must match Focused's JSON keys exactly. Unknown keys in the
    // response are ignored by JsonUtility, which is why Focused can grow its
    // payload without breaking older plugins.

    [Serializable]
    public sealed class RulePayload
    {
        public string[] names;
        public string[] cmdline_substrings;
        public int[] pids;
        public string[] protect_names;
        public string[] protect_cmdline_substrings;
    }

    /// <summary>
    /// One heartbeat: whether a session is running, and how long Focused may keep
    /// believing it.
    /// </summary>
    /// <remarks>
    /// The lease is the point. The plugin can be killed (or frozen) mid-session, and
    /// Focused has no way to ask it anything ever again; without an expiry the
    /// applications it suspended would stay suspended with nobody left to resume
    /// them.
    /// </remarks>
    [Serializable]
    public sealed class SessionStatePayload
    {
        public bool active;
        public bool dry_run;
        public string source;
        public int ttl_seconds;
    }

    [Serializable]
    public sealed class RulesResponse
    {
        // Focused's "applied" block is a nested object, which JsonUtility does not
        // bind here; the plugin only needs to know the push was accepted.
        public bool ok;
    }

    /// <summary>
    /// What <c>GET /api/v1/focus</c> returns, as the plugin reads it.
    /// </summary>
    /// <remarks>
    /// Flat on purpose: Unity's JsonUtility binds public fields and primitive
    /// arrays, so a nested object arrives as null. Focused keeps this payload flat
    /// for exactly that reason, and <c>FocusedPayloadTests</c> fails the build if a
    /// field the plugin needs is not there.
    /// </remarks>
    [Serializable]
    public sealed class FocusStateResponse
    {
        public bool ok;
        public string version;
        public int pid;
        public bool active;
        public bool paused;
        public float pause_until;
        public float since;
        public bool dry_run;
        public bool manual_session;
        public bool delegated;
        public bool delegated_active;
        public string delegated_source;
        public int frozen_count;
        public int frozen_total;
        public string[] frozen_names;
        public string[] frozen_units;
        public string[] recent_names;
        public string[] url_patterns;
        public int protect_count;
    }

    [Serializable]
    public sealed class EventListResponse
    {
        public bool ok;
        public int next_seq;
    }

    [Serializable]
    public sealed class ScanResponse
    {
        // "outcomes" is an array of custom classes, which JsonUtility does not bind here;
        // the caller only needs to know the scan ran.
        public bool ok;
    }

    /// <summary>One row of the running-process picker, built from the arrays above.</summary>
    public sealed class ProcessItem
    {
        public string name;
        public int count;
        public bool @protected;
        public string protect_rule;
    }

    /// <summary>
    /// The running-process list, in the only shape Unity's JsonUtility binds: parallel
    /// arrays of primitives.
    /// </summary>
    /// <remarks>
    /// Focused also sends an array of objects for its own dashboard. It arrives here as
    /// null -- the fallback that used to read it was never taken -- so this DTO does not
    /// declare it, and the contract test keeps every response DTO in this shape.
    /// </remarks>
    [Serializable]
    public sealed class ProcessListResponse
    {
        public bool ok;
        public string[] names;
        public int[] counts;
        public bool[] protected_flags;
        public string[] protect_rules;

        /// <summary>The list as items.</summary>
        public ProcessItem[] ToItems()
        {
            if (names == null || names.Length == 0)
            {
                return new ProcessItem[0];
            }

            var items = new ProcessItem[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                items[i] = new ProcessItem
                {
                    name = names[i],
                    count = counts != null && index < counts.Length ? counts[index] : 1,
                    @protected = protected_flags != null && index < protected_flags.Length
                                 && protected_flags[index],
                    protect_rule = protect_rules != null && index < protect_rules.Length
                        ? protect_rules[index]
                        : string.Empty,
                };
            }

            return items;
        }
    }

    /// <summary>For endpoints whose only useful field is <c>ok</c>.</summary>
    [Serializable]
    public sealed class SimpleResponse
    {
        public bool ok;
        public string detail;
        public string error;
    }
}
