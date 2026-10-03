using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>The styles the picker borrows from the window's theme.</summary>
    /// <remarks>
    /// A bundle rather than a reference to the theme: the window rebuilds its styles
    /// whenever the layout file changes, and this is what it hands over each time.
    /// </remarks>
    internal sealed class PickerStyles
    {
        internal GUIStyle Row;
        internal GUIStyle Hint;
        internal GUIStyle Header;
        internal GUIStyle Blocked;
    }

    /// <summary>
    /// The app lists: what is blocked right now, and everything currently running.
    /// </summary>
    /// <remarks>
    /// Split out of the settings window, which keeps the frame, the status line and the
    /// advanced page. This half owns everything a list needs -- the fetch, the filter,
    /// the two scroll positions and the block/protect mode -- so the window only has to
    /// say where to draw it.
    /// </remarks>
    internal sealed class AppPicker
    {
        private const float RefreshSeconds = 6f;
        private const float BlockedListMaxHeight = 170f;

        private readonly PickerStyles _styles;
        private readonly Action<string> _log;
        private readonly Action<string, bool> _toast;

        private Vector2 _processScroll;
        private string _filter = string.Empty;
        private ProcessItem[] _processes = new ProcessItem[0];
        private string _processError;
        private float _nextRefresh;
        private float _fetchedAt;
        private bool _fetching;
        private int _lastLoggedCount = -1;
        private bool _blacklistExpanded = true;
        private Vector2 _blacklistScroll;

        /// <summary>False: clicking a row toggles the block list. True: the protect list.</summary>
        private bool _protectMode;

        internal AppPicker(PickerStyles styles, Action<string> log, Action<string, bool> toast)
        {
            _styles = styles;
            _log = log;
            _toast = toast;
        }

        /// <summary>The last list-fetch failure, for the advanced page's diagnostics block.</summary>
        internal string ProcessError
        {
            get { return _processError; }
        }

        /// <summary>The blocked list, the picker, and the note under them.</summary>
        internal void Draw(ConfigBridge cfg, FocusedClient client, PanelSpec spec, float windowHeight)
        {
            DrawBlocked(cfg, spec, windowHeight);
            GUILayout.Space(8f);
            DrawRunning(cfg, client, spec, windowHeight);

            GUILayout.Label(PanelStrings.S("panel.protected_names_hint", "被保护名单覆盖的名字无法冻结 —— wineserver、steam、桌面组件等。"), _styles.Hint);
            GUILayout.Space(10f);
        }

        private void DrawBlocked(ConfigBridge cfg, PanelSpec spec, float windowHeight)
        {
            var names = spec.Previewing && spec.PreviewApps > 0
                ? new List<string>(spec.PreviewNames())
                : cfg.Names();
            var arrow = _blacklistExpanded ? "\u25be " : "\u25b8 ";

            // A disclosure header rather than a plain label: with sixty apps in the
            // picker below, the short list of what is actually blocked is the thing
            // worth folding out on its own.
            if (GUILayout.Button(
                    arrow + PanelStrings.S("panel.apps_to_block", "要冻结的应用") + "   (" + names.Count + ")",
                    _styles.Header))
            {
                _blacklistExpanded = !_blacklistExpanded;
            }

            if (_blacklistExpanded)
            {
                if (names.Count == 0)
                {
                    GUILayout.Label(PanelStrings.S("panel.nothing_is_blocked_yet_pick_one_from_the", "   还没有要冻结的应用 —— 从下面的列表里选一个。"), _styles.Hint);
                }
                else
                {
                    // Grows with the list up to a cap, then scrolls. Sibling of the
                    // picker's scroll view, not nested inside it: nesting collapses
                    // the inner one to zero height in IMGUI.
                    var height = BlockedHeight(spec, windowHeight, names.Count);
                    _blacklistScroll = GUILayout.BeginScrollView(
                        _blacklistScroll, GUILayout.Height(height));

                    foreach (var name in new List<string>(names))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("   " + name, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("\u2715", GUILayout.Width(30f)))
                        {
                            if (Writable(spec))
                            {
                                cfg.RemoveName(name);
                                Report("toast.removed", "已移除", name);
                            }
                            else
                            {
                                PreviewNotice();
                            }
                        }

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.EndScrollView();
                }
            }
            else if (names.Count > 0)
            {
                // Collapsed: a bounded summary, so a long list cannot push the panel
                // off the screen on its own.
                var shown = names.Count > 6 ? names.GetRange(0, 6) : names;
                var summary = string.Join(" · ", shown.ToArray());
                if (names.Count > 6)
                {
                    summary += "  +" + (names.Count - 6);
                }

                GUILayout.Label("   " + summary, _styles.Hint);
            }
        }

        private void DrawRunning(ConfigBridge cfg, FocusedClient client, PanelSpec spec, float windowHeight)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(PanelStrings.S("panel.running_now_click_a_row_to_add_or_remove", "当前运行中 —— 点击一行即可加入/移除："), GUILayout.ExpandWidth(true));
            GUILayout.Label(
                _fetchedAt > 0f
                    ? PanelStrings.Sf("panel.found_n", "已获取 {0} 个", _processes.Length)
                    : PanelStrings.S("panel.not_fetched", "尚未获取"),
                _styles.Hint, GUILayout.Width(100f));
            if (GUILayout.Button(_protectMode
                    ? PanelStrings.S("panel.mode_protect", "保护模式")
                    : PanelStrings.S("panel.mode_block", "冻结模式"),
                    GUILayout.Width(80f)))
            {
                _protectMode = !_protectMode;
            }

            if (GUILayout.Button(PanelStrings.S("panel.refresh", "刷新"), GUILayout.Width(74f)))
            {
                _nextRefresh = 0f;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(PanelStrings.S("panel.filter", "过滤"), GUILayout.Width(44f));
            _filter = GUILayout.TextField(_filter ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
            {
                _filter = string.Empty;
            }

            GUILayout.EndHorizontal();

            RequestProcessesIfNeeded(client);

            if (!string.IsNullOrEmpty(_processError))
            {
                GUILayout.Label(PanelStrings.S("panel.could_not_read_the_list", "无法读取列表：") + _processError,
                                _styles.Hint);
            }

            // What the click actually does, spelled out. The mode button alone said
            // "block mode" and "protect mode", which named the state without saying
            // what clicking a row would then do to it.
            GUILayout.Label(_protectMode
                    ? PanelStrings.S("panel.hint_protect_mode", "保护模式 —— 点中的进程永不被冻结。受保护的进程平时不出现在这个列表里，所以这里是取消保护的地方。")
                    : PanelStrings.S("panel.hint_block_mode", "冻结模式 —— 点中的进程会在游戏计时运行期间被冻结；再点一次移出名单。"),
                _styles.Hint);

            _processScroll = GUILayout.BeginScrollView(
                _processScroll, GUILayout.Height(PickerHeight(spec, windowHeight)));

            var items = spec.PreviewProcesses > 0 ? spec.PreviewItems() : _processes;

            var shown = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.name) || !AppFilter.Matches(_filter, item.name))
                {
                    continue;
                }

                shown++;

                if (item.@protected)
                {
                    // Hidden from the block list, because a protected name can never be
                    // blocked and offering the row only invited a click that could not
                    // work. It comes back in protect mode, where these are exactly the
                    // rows you click to take a name back off the protect list.
                    if (!_protectMode && spec.HideProtected > 0)
                    {
                        continue;
                    }

                    var wasEnabled = GUI.enabled;
                    GUI.enabled = false;
                    GUILayout.Button("   " + item.name + "        " +
                                     PanelStrings.S("panel.protected", "受保护"), _styles.Row);
                    GUI.enabled = wasEnabled;
                    continue;
                }

                var listed = _protectMode ? cfg.HasProtectName(item.name) : cfg.HasName(item.name);

                // No tick prefix: an already-blocked app is coloured instead, like the
                // protected rows, which also keeps every name at the same indent.
                var label = "   " + item.name +
                            (item.count > 1 ? "   \u00d7" + item.count : string.Empty);

                if (GUILayout.Button(label, listed ? _styles.Blocked : _styles.Row) && Writable(spec))
                {
                    if (listed)
                    {
                        RemoveRow(cfg, item.name);
                        Report("toast.removed", "已移除", item.name);
                    }
                    else
                    {
                        AddRow(cfg, item.name);
                        Report("toast.added", "已加入", item.name);
                    }
                }
            }

            if (shown == 0)
            {
                GUILayout.Label(_fetching
                    ? PanelStrings.S("panel.asking_the_executor", "  正在向后台执行器查询…")
                    : PanelStrings.S("panel.nothing_matches", "  （没有匹配的进程）"), _styles.Hint);
            }

            GUILayout.EndScrollView();
        }

        /// <summary>Route a row click to the list the picker is currently editing.</summary>
        private void AddRow(ConfigBridge cfg, string name)
        {
            if (_protectMode)
            {
                cfg.AddProtectName(name);
            }
            else
            {
                cfg.AddName(name);
            }
        }

        private void RemoveRow(ConfigBridge cfg, string name)
        {
            if (_protectMode)
            {
                cfg.RemoveProtectName(name);
            }
            else
            {
                cfg.RemoveName(name);
            }
        }

        private void RequestProcessesIfNeeded(FocusedClient client)
        {
            if (client == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextRefresh || _fetching || client.Busy)
            {
                return;
            }

            _fetching = true;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            client.GetProcessesAsync(400, (response, error) =>
            {
                _fetching = false;
                if (error != null || response == null || !response.ok)
                {
                    _processError = error ?? "unexpected response";
                    if (_lastLoggedCount != -2)
                    {
                        _lastLoggedCount = -2;
                        Report("process list FAILED: " + _processError);
                    }

                    return;
                }

                _processError = null;

                // Prefers Focused's parallel primitive arrays: Unity's JsonUtility
                // silently returned null for the array of objects, which left this
                // list empty with no error anywhere.
                _processes = response.ToItems();
                _fetchedAt = Time.unscaledTime;

                if (_lastLoggedCount != _processes.Length)
                {
                    _lastLoggedCount = _processes.Length;
                    Report("process list: " + _processes.Length + " name(s)");
                }
            });
        }

        /// <summary>
        /// Heights that adapt to the window.
        /// </summary>
        /// <remarks>
        /// The main view deliberately has no scroll view of its own -- the two lists
        /// each need one, and nesting collapses the inner one to zero height. So the
        /// lists shrink to fit instead of overflowing off the screen.
        /// </remarks>
        private static float BlockedHeight(PanelSpec spec, float windowHeight, int count)
        {
            var cap = Mathf.Clamp(windowHeight * 0.24f, 70f, spec.BlockedListMaxHeight);
            return Mathf.Min(cap, count * 26f + 10f);
        }

        private static float PickerHeight(PanelSpec spec, float windowHeight)
        {
            return Mathf.Clamp(windowHeight - 430f, 120f, spec.PickerListMaxHeight);
        }

        /// <summary>False while the panel is showing test data.</summary>
        private static bool Writable(PanelSpec spec)
        {
            return !spec.Previewing;
        }

        private void PreviewNotice()
        {
            if (_toast != null)
            {
                _toast(PanelStrings.S("panel.preview_mode_nothing_was_changed", "预览模式 —— 未做任何修改"), false);
            }
        }

        private void Report(string key, string chinese, string name)
        {
            var message = PanelStrings.Notice(key, chinese, name);
            if (_toast != null)
            {
                _toast(message, false);
            }
        }

        private void Report(string message)
        {
            if (_log != null)
            {
                _log(message);
            }
        }
    }
}
