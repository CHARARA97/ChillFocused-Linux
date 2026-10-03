using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The actions the advanced page can ask for.
    /// </summary>
    /// <remarks>
    /// Read at the moment a control is used rather than captured when the page is built:
    /// the runner wires these after the panel has been constructed. A missing hook means
    /// the action cannot happen, which is also why its confirmation toast is skipped.
    /// </remarks>
    internal interface IAdvancedHooks
    {
        /// <summary>Record only, do not terminate.</summary>
        Action<bool> SetDryRun { get; }

        /// <summary>Ask Focused to follow the game timer, or to ignore it.</summary>
        Action<bool> SetGameGate { get; }

        /// <summary>Resume everything Focused suspended, right now.</summary>
        Action ThawNow { get; }

        /// <summary>Scan immediately.</summary>
        Action ScanNow { get; }

        /// <summary>Re-read the panel layout file now.</summary>
        Action ReloadSpec { get; }

        /// <summary>Short message for the overlay: (text, warning).</summary>
        Action<string, bool> Toast { get; }
    }

    /// <summary>
    /// What the advanced page reports: Focused's state, and the two error strings it
    /// shows under "diagnostics".
    /// </summary>
    internal struct AdvancedStatus
    {
        internal bool Connected;
        internal string Version;
        internal string Url;
        internal bool DryRun;
        internal bool GateKnown;
        internal bool GateEnabled;
        internal bool FreezeKnown;
        internal bool FreezeEnabled;
        internal int FrozenCount;
        internal string FrozenNames;
        internal string ProcessError;
        internal string LastRawError;

        /// <summary>What the plugin is and which game language it settled on.</summary>
        internal string PluginVersion;
        internal string GameLanguage;
        internal bool GameLanguageFromGame;
        internal bool ContainerFound;
    }

    /// <summary>
    /// The advanced page: launch-argument matching, behaviour, display, the layout file,
    /// and the executor's diagnostics.
    /// </summary>
    /// <remarks>
    /// Split out of the settings window, which keeps the frame, the status line and the
    /// two lists. Everything here is a control plus a callout, so the page itself owns no
    /// state beyond the half-typed command line.
    /// </remarks>
    internal sealed class AdvancedPane
    {
        private readonly PanelStyles _styles;
        private readonly IAdvancedHooks _hooks;

        // Its own state: what the user has typed but not yet added.
        private string _newCmdline = string.Empty;

        internal AdvancedPane(PanelStyles styles, IAdvancedHooks hooks)
        {
            _styles = styles;
            _hooks = hooks;
        }

        internal void Draw(ConfigBridge cfg, PanelSpec spec, AdvancedStatus status, float windowWidth)
        {
            GUILayout.Label(PanelStrings.S("panel.advanced", "高级选项"), _styles.Headline);
            GUILayout.Label(PanelStrings.S("panel.these_apply_immediately_and_are_written_", "以下设置立即生效，并会写入配置文件。"), _styles.Hint);
            GUILayout.Space(8f);

            // -- matching --------------------------------------------------
            var values = cfg.Cmdlines();
            GUILayout.Label(PanelStrings.S("panel.match_by_launch_arguments", "按启动参数匹配") + "  (" +
                            values.Count + ")", _styles.Section);
            GUILayout.Label(PanelStrings.S("panel.matches_the_whole_command_line_so_one_in", "匹配完整命令行，因此可以只冻结某个应用的其中一个实例。"), _styles.Hint);

            foreach (var value in new List<string>(values))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("   " + value, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("\u2715", GUILayout.Width(28f)))
                {
                    cfg.RemoveCmdline(value);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            _newCmdline = GUILayout.TextField(_newCmdline ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(PanelStrings.S("panel.add", "加入"), GUILayout.Width(74f)))
            {
                cfg.AddCmdline(_newCmdline);
                _newCmdline = string.Empty;
            }

            GUILayout.EndHorizontal();
            _styles.DrawDivider(windowWidth - 64f);

            // -- behaviour -------------------------------------------------
            GUILayout.Label(PanelStrings.S("panel.behaviour", "行为"), _styles.Section);

            var dry = _styles.Checkbox(PanelStrings.S("panel.record_only_do_not_close_anything", "只记录，不冻结任何进程"), status.DryRun, spec);
            var setDryRun = _hooks.SetDryRun;
            if (dry != status.DryRun && setDryRun != null)
            {
                setDryRun(dry);
                Report("toast.record_only", "只记录不冻结", dry ? "on" : "off");
            }

            if (status.FreezeKnown && !status.FreezeEnabled)
            {
                // Freezing is the only action now, so this is a warning rather than
                // a switch: somebody turned it off by editing the JSON by hand.
                GUILayout.Label(
                    PanelStrings.S("panel.freeze_off_warning",
                                   "注意：冻结已被配置文件关闭（freeze.enabled=false），应用不会被冻结。"),
                    _styles.Hint);
            }

            if (status.FrozenCount > 0)
            {
                GUILayout.Label(
                    PanelStrings.S("overlay.frozen", "已冻结") + " · " + status.FrozenNames,
                    _styles.Hint);
                var thawNow = _hooks.ThawNow;
                if (thawNow != null && GUILayout.Button(
                        PanelStrings.S("panel.thaw_now", "立即恢复全部"), GUILayout.Width(160f)))
                {
                    thawNow();
                    Report("toast.thaw", "恢复", "all");
                }
            }

            var ignoreTimer = _styles.Checkbox(
                PanelStrings.S("panel.do_not_depend_on_the_game_s_timer_always", "不依赖游戏计时器（始终生效）"), status.GateKnown && !status.GateEnabled, spec);
            var setGameGate = _hooks.SetGameGate;
            if (status.GateKnown && ignoreTimer == status.GateEnabled && setGameGate != null)
            {
                setGameGate(!ignoreTimer);
                Report("toast.always_on", "始终生效", ignoreTimer ? "on" : "off");
            }

            GUILayout.Space(8f);

            // -- display ---------------------------------------------------
            GUILayout.Label(PanelStrings.S("panel.display", "显示"), _styles.Section);

            var showHud = _styles.Checkbox(
                PanelStrings.S("panel.show_the_overlay_f9_cycles_it", "显示状态面板（F9 循环切换）"),
                cfg.Hud != HudMode.Off, spec);
            if (showHud != (cfg.Hud != HudMode.Off))
            {
                cfg.Hud = showHud ? HudMode.Minimal : HudMode.Off;
            }

            var detailed = _styles.Checkbox(PanelStrings.S("panel.detailed_overlay", "详细状态面板"),
                                            cfg.Hud == HudMode.Detailed, spec);
            if (detailed != (cfg.Hud == HudMode.Detailed))
            {
                cfg.Hud = detailed ? HudMode.Detailed : HudMode.Minimal;
            }

            GUILayout.Label(PanelStrings.Sf("panel.font", "字体：{0}", HudFont.Resolve(spec.FontFamily).name), _styles.Hint);
            GUILayout.Space(8f);

            // -- layout file -----------------------------------------------
            GUILayout.Label(PanelStrings.S("panel.panel_layout_file", "面板布局文件"), _styles.Section);
            GUILayout.Label("   " + (string.IsNullOrEmpty(spec.Source) ? "?" : spec.Source),
                            _styles.Hint);
            GUILayout.Label(PanelStrings.S("panel.layout_file_hint", "编辑并保存即可，面板 1 秒内自动重新加载，无需重启。把 preview.apps 或 " +
                "preview.processes 设为大于 0 可用测试数据检查布局。"), _styles.Hint);
            if (GUILayout.Button(PanelStrings.S("panel.reload_panel_config", "重新加载面板配置"),
                                 GUILayout.Width(200f)))
            {
                var reload = _hooks.ReloadSpec;
                if (reload != null)
                {
                    reload();
                }
            }

            GUILayout.Space(8f);

            // -- executor --------------------------------------------------
            GUILayout.Label(PanelStrings.S("panel.executor", "后台执行器"), _styles.Section);
            GUILayout.Label(
                PanelStrings.S("panel.status", "状态") + "  ·  " +
                (status.Connected
                    ? PanelStrings.S("panel.connected_v", "已连接 v") + status.Version
                    : PanelStrings.S("panel.not_connected", "未连接")),
                _styles.Hint);

            if (!string.IsNullOrEmpty(status.Url))
            {
                GUILayout.Label(PanelStrings.S("panel.address", "地址") + "  ·  " + status.Url, _styles.Hint);
            }

            if (GUILayout.Button(PanelStrings.S("panel.scan_now", "立即扫描"), GUILayout.Width(120f)))
            {
                var scan = _hooks.ScanNow;
                if (scan != null)
                {
                    scan();
                }

                Report("toast.scanning", "正在扫描", string.Empty);
            }

            // The raw text lives only here, never on the overlay: it is for a bug
            // report, not for a player.
            // Always shown, not only on an error: "which build is this, which
            // language did it settle on, and did the game's service container answer"
            // are the first three questions any bug report asks, and the reporter
            // cannot answer them from the panel otherwise.
            GUILayout.Space(6f);
            GUILayout.Label(PanelStrings.S("panel.diagnostics_for_a_bug_report", "诊断信息（用于反馈问题）："), _styles.Hint);
            GUILayout.Label("   ChillFocused " + (status.PluginVersion ?? "?"), _styles.Hint);
            GUILayout.Label(
                "   panel language: " + (status.GameLanguage ?? "?") +
                (status.GameLanguageFromGame ? " (from the game)" : " (default, not detected)"),
                _styles.Hint);
            GUILayout.Label(
                "   game services (DI): " + (status.ContainerFound ? "found" : "not found - using method hooks"),
                _styles.Hint);
            if (!string.IsNullOrEmpty(status.LastRawError))
            {
                GUILayout.Label("   " + status.LastRawError, _styles.Hint);
            }
        }

        private void Report(string key, string chinese, string name)
        {
            var toast = _hooks.Toast;
            if (toast != null)
            {
                toast(PanelStrings.Notice(key, chinese, name), false);
            }
        }
    }
}
