using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>
    /// An immutable-enough snapshot of the plugin configuration.
    /// </summary>
    /// <remarks>
    /// Unity-free on purpose (see <see cref="RuleText"/>), so the mapping from user
    /// configuration to Focused's rule payload -- and the session decision itself --
    /// are unit tested without the game.
    /// </remarks>
    public sealed class FocusSettings
    {
        public HudMode Hud = HudMode.Minimal;
        public string HudCorner = "top-left";
        public float ToastSeconds = 4f;
        public float PollIntervalSeconds = 1f;

        /// <summary>Focused's loopback API. It is the only backend there is.</summary>
        public string FocusedUrl = "http://127.0.0.1:8766";
        public string FocusedToken = string.Empty;
        public int RequestTimeoutMs = 1500;

        /// <summary>The master switch: with it off, nothing is ever suspended.</summary>
        public bool Enabled = true;

        /// <summary>Ignore the game's timer and stay active while the game runs.</summary>
        public bool AlwaysOn;

        /// <summary>Record what would be suspended, but suspend nothing.</summary>
        public bool DryRun;

        /// <summary>How long the game's last timer reading stays believable.</summary>
        public float TimerFreshSeconds = SessionGate.DefaultFreshSeconds;

        /// <summary>How long Focused may act on one heartbeat. Renewed while active.</summary>
        public int SessionLeaseSeconds = 30;
        public string[] ProcessNames = new string[0];
        public string[] CmdlineSubstrings = new string[0];
        public string[] ProtectedNames = new string[0];
        public string[] ProtectedCmdlineSubstrings = new string[0];

        /// <summary>
        /// Build the payload for <c>PUT /api/v1/focus/rules</c>.
        /// </summary>
        /// <remarks>
        /// Focused keeps one rule set per source and merges them, so this list is
        /// this plugin's own contribution: sending it in full replaces what *this
        /// plugin* said last time and cannot touch anybody else's rules. The protect
        /// fields are merely additions -- Focused unions them with the built-in
        /// rails, so an empty array (Unity emits that for a null array) can never
        /// strip the protections that keep the game and the desktop alive.
        /// </remarks>
        public RulePayload ToRulePayload()
        {
            return new RulePayload
            {
                names = ProcessNames ?? new string[0],
                cmdline_substrings = CmdlineSubstrings ?? new string[0],
                pids = new int[0],
                protect_names = ProtectedNames ?? new string[0],
                protect_cmdline_substrings = ProtectedCmdlineSubstrings ?? new string[0],
            };
        }

        /// <summary>
        /// Serialise the rule payload without Unity's JsonUtility.
        /// </summary>
        /// <remarks>
        /// Needed because the sync thread runs off the main thread, and because
        /// the rest of the plugin must keep working even when Unity never gives
        /// it a frame.
        /// </remarks>
        public string BuildRulesJson()
        {
            return RuleJson.Build(this);
        }

        /// <summary>Problems worth telling the user about; empty when healthy.</summary>
        public string[] Validate()
        {
            var problems = new List<string>();

            var names = ProcessNames ?? new string[0];
            for (var i = 0; i < names.Length; i++)
            {
                if (RuleText.IsCatchAllPattern(names[i]))
                {
                    problems.Add(
                        "ProcessNames contains the catch-all pattern '" + names[i] +
                        "'; Focused will reject the whole rule set");
                }
            }

            var substrings = CmdlineSubstrings ?? new string[0];
            for (var i = 0; i < substrings.Length; i++)
            {
                if (RuleText.IsCatchAllPattern(substrings[i]))
                {
                    problems.Add(
                        "CmdlineSubstrings contains the catch-all pattern '" + substrings[i] +
                        "'; Focused will reject the whole rule set");
                }
            }

            if (PollIntervalSeconds < 0.2f)
            {
                problems.Add("PollIntervalSeconds is below the 0.2s floor");
            }

            if (RequestTimeoutMs < 100)
            {
                problems.Add("RequestTimeoutMs is below the 100ms floor");
            }

            if (string.IsNullOrEmpty(FocusedUrl))
            {
                problems.Add("FocusedUrl is empty");
            }

            if (SessionLeaseSeconds < 5)
            {
                problems.Add("SessionLeaseSeconds is below the 5s floor");
            }

            if (TimerFreshSeconds < 5f)
            {
                problems.Add("TimerFreshSeconds is below the 5s floor");
            }

            return problems.ToArray();
        }

        /// <summary>Everything that changes the session decision or the heartbeat.</summary>
        internal bool SessionEquals(FocusSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && AlwaysOn == other.AlwaysOn
                && DryRun == other.DryRun
                && TimerFreshSeconds == other.TimerFreshSeconds
                && SessionLeaseSeconds == other.SessionLeaseSeconds;
        }

        /// <summary>The session decision, using this snapshot's own switches.</summary>
        internal bool SessionActive(TimerState timer, float now)
        {
            return SessionGate.Active(Enabled, AlwaysOn, timer, now, TimerFreshSeconds);
        }

        public bool RulesEqual(FocusSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return SameStrings(ProcessNames, other.ProcessNames)
                && SameStrings(CmdlineSubstrings, other.CmdlineSubstrings)

                && SameStrings(ProtectedNames, other.ProtectedNames)
                && SameStrings(ProtectedCmdlineSubstrings, other.ProtectedCmdlineSubstrings);
        }

        public bool EndpointEquals(FocusSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(FocusedUrl, other.FocusedUrl, StringComparison.Ordinal)
                && string.Equals(FocusedToken, other.FocusedToken, StringComparison.Ordinal)
                && RequestTimeoutMs == other.RequestTimeoutMs;
        }

        private static bool SameStrings(string[] a, string[] b)
        {
            a = a ?? new string[0];
            b = b ?? new string[0];
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
