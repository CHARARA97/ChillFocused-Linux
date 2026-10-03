using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>
    /// Resolves one of the overlay's text keys.
    /// </summary>
    /// <remarks>
    /// Matches the runner's own <c>T</c>: the caller decides which user-editable table the
    /// key resolves through, so this file stays free of it.
    /// </remarks>
    public delegate string TextLookup(string key, string chinese);

    /// <summary>
    /// The status copy for both surfaces: which sentence describes the current state, how
    /// the most recently closed process names are listed, and the settings window's
    /// headline.
    /// </summary>
    /// <remarks>
    /// Extracted from the runner because this is string work over plain state, with no
    /// Unity types in sight: the text lookup and the inputs arrive as arguments, so every
    /// branch can be tested without a running game.
    /// </remarks>
    public static class HudState
    {
        /// <summary>The one sentence that answers "is it working right now?".</summary>
        public static string StateReason(TextLookup text, bool connected, bool enforcing, bool gateSatisfied)
        {
            if (!connected)
            {
                return Resolve(text, "overlay.not_enforcing_executor_not_connected", "未生效 · 后台执行器未连接");
            }

            if (enforcing && gateSatisfied)
            {
                return Resolve(text, "overlay.enforcing_work_session", "生效中 · 创作模式");
            }

            if (enforcing)
            {
                return Resolve(text, "overlay.enforcing", "生效中");
            }

            return Resolve(text, "overlay.not_enforcing_start_the_game_s_timer", "未生效 · 开始游戏计时后自动生效");
        }

        /// <summary>
        /// The settings window's headline: the same question as
        /// <see cref="StateReason"/>, answered from Focused's view of the gate
        /// rather than the plugin's, and prefixed with a filled or hollow dot.
        /// </summary>
        public static string PanelHeadline(TextLookup text, bool connected, bool gateKnown,
                                           bool gateEnabled, bool gateSatisfied)
        {
            // One sentence, one dot: the whole answer the player needs.
            if (!connected)
            {
                return "\u25cb " + Resolve(text, "panel.not_active_executor_offline", "未生效 · 后台执行器未连接");
            }

            if (gateKnown && !gateEnabled)
            {
                return "\u25cf " + Resolve(text, "panel.active_always_on", "生效中 · 不依赖计时器");
            }

            if (gateKnown && gateSatisfied)
            {
                return "\u25cf " + Resolve(text, "panel.active_work_session_running", "生效中 · 创作模式");
            }

            return "\u25cb " + Resolve(text, "panel.waiting_start_the_game_s_timer", "待机 · 开始游戏计时后自动生效");
        }

        /// <summary>
        /// The most recently closed process names, joined into one HUD line. Empty
        /// entries are skipped and do not count towards <paramref name="wanted"/>.
        /// </summary>
        /// <remarks>
        /// A plain string array because that is the only shape Unity's JsonUtility binds:
        /// Focused used to send a list of objects here and it always arrived empty.
        /// </remarks>
        public static string RecentNames(string[] recent, int wanted)
        {
            if (recent == null || wanted < 1)
            {
                return string.Empty;
            }

            var names = new List<string>();
            for (var i = 0; i < recent.Length && names.Count < wanted; i++)
            {
                if (!string.IsNullOrEmpty(recent[i]))
                {
                    names.Add(recent[i]);
                }
            }

            return string.Join(" · ", names.ToArray());
        }

        /// <summary>
        /// One key through the caller's lookup; a missing lookup falls back to the
        /// built-in wording rather than throwing mid-frame.
        /// </summary>
        public static string Resolve(TextLookup text, string key, string chinese)
        {
            return text != null ? text(key, chinese) : chinese;
        }
    }
}
