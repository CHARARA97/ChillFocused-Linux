using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The in-game settings window: its frame, its status line, and the two views it
    /// switches between.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Structured around one idea: the player's only real question is "is it
    /// intercepting right now", and the only real task is "pick the apps to close".
    /// Everything else -- the executor's address, dry-run, the font, the raw error
    /// text -- lives behind <em>Advanced</em>, which is collapsed but genuinely
    /// reachable rather than hidden.
    /// </para>
    /// <para>
    /// Only the frame is here. The look lives in <see cref="PanelStyles"/>, the two lists
    /// in <see cref="AppPicker"/>, and the advanced page in <see cref="AdvancedPane"/> --
    /// this class owns the window rectangle, the backend state the runner pushes in, and
    /// the hooks those parts call back through.
    /// </para>
    /// <para>
    /// There is no focus switch. Enforcement follows the game's own timer, so a
    /// second control would only be something to reconcile.
    /// </para>
    /// <para>
    /// Rows in the pick-list are buttons rather than labels, so clicking anywhere on
    /// a row toggles it. The window is drawn with IMGUI, so clicks on it can also
    /// reach the game behind it -- inherent to overlaying a game without patching
    /// its input system. The window is draggable to somewhere harmless.
    /// </para>
    /// </remarks>
    internal sealed class SettingsPanel : IAdvancedHooks
    {
        private const int WindowId = 0x0F0C05;

        /// <summary>Where diagnostics go; the runner points this at BepInEx's log.</summary>
        internal static Action<string> Log;

        // -- supplied by the runner -------------------------------------------

        /// <summary>Short message for the overlay: (text, warning).</summary>
        internal Action<string, bool> Toast;

        /// <summary>Ask Focused to follow the game timer, or to ignore it.</summary>
        internal Action<bool> SetGameGate;

        /// <summary>Record only, do not terminate.</summary>
        internal Action<bool> SetDryRun;

        /// <summary>Resume everything Focused suspended, right now.</summary>
        internal Action ThawNow;

        /// <summary>Scan immediately.</summary>
        internal Action ScanNow;

        /// <summary>Re-read the panel layout file now.</summary>
        internal Action ReloadSpec;

        // -- window state ------------------------------------------------------

        internal bool Open { get; set; }

        private Rect _rect = new Rect(340f, 20f, 640f, 700f);
        private string _lastRawError = string.Empty;
        private bool _advanced;
        private Vector2 _advancedScroll;

        // Live layout values. Loaded from the panel spec file and hot-reloaded,
        // so adjusting the panel costs an edit rather than a rebuild and restart.
        private PanelSpec _spec = PanelSpec.Defaults();
        private readonly PanelStyles _styles = new PanelStyles();

        // The two lists, drawn with the styles bundle the theme refills.
        private readonly AppPicker _picker;

        // The advanced page, which reads the hooks below through IAdvancedHooks.
        private readonly AdvancedPane _advancedPane;

        // The last rectangle taken from the spec. Remembering the *applied* values,
        // rather than a one-shot flag, is what lets a hot-reload move the window while
        // still leaving a window the user has dragged where they put it: an edit that
        // does not touch these four keys applies nothing.
        private bool _rectFromSpec;
        private float _appliedX, _appliedY, _appliedWidth, _appliedHeight;

        // -- backend state, pushed in by the runner -----------------------------

        private bool _connected;
        private bool _dryRun;
        private string _focusedVersion = "?";
        private string _backendUrl = string.Empty;
        private bool _gateKnown;
        private bool _gateEnabled = true;
        private bool _gateSatisfied;
        private bool _freezeKnown;
        private bool _freezeEnabled;
        private int _frozenCount;
        private string _frozenNames = string.Empty;

        internal SettingsPanel()
        {
            // The delegates read these fields when they fire rather than capturing them
            // now: the runner wires Toast after the panel has been constructed.
            _picker = new AppPicker(
                _styles.Picker,
                message =>
                {
                    var log = Log;
                    if (log != null)
                    {
                        log(message);
                    }
                },
                (message, warning) =>
                {
                    if (Toast != null)
                    {
                        Toast(message, warning);
                    }
                });

            // Handed "this" rather than the five delegates, because IAdvancedHooks reads
            // them on demand and the runner wires them after construction.
            _advancedPane = new AdvancedPane(_styles, this);
        }

        // -- the advanced page's hooks -----------------------------------------
        // Read at the moment a control is used, so a hook the runner has not wired yet is
        // visible as null rather than as a stale copy.

        Action<bool> IAdvancedHooks.SetDryRun
        {
            get { return SetDryRun; }
        }

        Action<bool> IAdvancedHooks.SetGameGate
        {
            get { return SetGameGate; }
        }

        Action IAdvancedHooks.ThawNow
        {
            get { return ThawNow; }
        }

        Action IAdvancedHooks.ScanNow
        {
            get { return ScanNow; }
        }

        Action IAdvancedHooks.ReloadSpec
        {
            get { return ReloadSpec; }
        }

        Action<string, bool> IAdvancedHooks.Toast
        {
            get { return Toast; }
        }

        internal void SetBackend(bool connected, bool dryRun, string version, string url)
        {
            _connected = connected;
            _dryRun = dryRun;
            _focusedVersion = version ?? "?";
            _backendUrl = url ?? string.Empty;
        }

        /// <summary>The game timer's state as Focused sees it.</summary>
        /// <summary>
        /// What Focused reports about freeze mode: whether the switch is on and
        /// what is suspended right now.
        /// </summary>
        internal void SetFrozenState(bool known, bool enabled, int count, string names)
        {
            _freezeKnown = known;
            _freezeEnabled = enabled;
            _frozenCount = count < 0 ? 0 : count;
            _frozenNames = names ?? string.Empty;
        }

        internal void SetGateState(bool known, bool enabled, bool satisfied)
        {
            _gateKnown = known;
            _gateEnabled = enabled;
            _gateSatisfied = satisfied;
        }

        /// <summary>
        /// Take a freshly loaded panel spec.
        /// </summary>
        /// <remarks>
        /// Styles are rebuilt against the new metrics. The window rectangle is
        /// only taken from the spec once, so a reload cannot yank a window the
        /// user has already dragged somewhere.
        /// </remarks>
        internal void SetSpec(PanelSpec spec)
        {
            if (spec == null)
            {
                return;
            }

            _spec = spec;
            _advanced = spec.StartAdvanced > 0;
            _styles.Rebuild();
        }

        /// <summary>Called by the runner when a request fails, for the diagnostics block.</summary>
        internal void SetRawError(string error)
        {
            _lastRawError = error ?? string.Empty;
        }

        // -- drawing -----------------------------------------------------------

        internal void Draw(ConfigBridge cfg, FocusedClient client, Texture2D background)
        {
            if (!Open || cfg == null)
            {
                return;
            }

            _styles.SetBackground(background);
            _styles.Ensure(_spec);

            if (!_rectFromSpec
                || _appliedX != _spec.WindowX
                || _appliedY != _spec.WindowY
                || _appliedWidth != _spec.WindowWidth
                || _appliedHeight != _spec.WindowHeight)
            {
                _rectFromSpec = true;
                _appliedX = _spec.WindowX;
                _appliedY = _spec.WindowY;
                _appliedWidth = _spec.WindowWidth;
                _appliedHeight = _spec.WindowHeight;

                _rect.x = _spec.WindowX;
                _rect.y = _spec.WindowY;
                _rect.width = _spec.WindowWidth;
                _rect.height = _spec.WindowHeight;
            }

            _rect.width = Mathf.Min(_rect.width, Screen.width - 20f);
            _rect.height = Mathf.Min(_rect.height, Screen.height - 40f);
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - _rect.height));

            _rect = GUI.Window(WindowId, _rect, id => DrawWindow(cfg, client), Title(), _styles.Window);
        }

        private string Title()
        {
            return PanelStrings.Override(_spec.TitleText, "ChillFocused 设置");
        }

        private void DrawWindow(ConfigBridge cfg, FocusedClient client)
        {
            // Two views rather than one expanding page. The advanced settings are
            // taller than any sensible window, and a scroll view cannot simply wrap
            // the lot: the process picker needs one of its own, and a scroll view
            // nested inside another collapses to zero height in IMGUI.
            GUILayout.BeginVertical();

            if (_advanced)
            {
                _advancedScroll = GUILayout.BeginScrollView(
                    _advancedScroll, GUILayout.ExpandHeight(true));
                _advancedPane.Draw(cfg, _spec, Status(), _rect.width);
                GUILayout.EndScrollView();
            }
            else
            {
                DrawStatus(cfg);
                _picker.Draw(cfg, client, _spec, _rect.height);
            }

            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            var arrow = _advanced ? "\u25c0 " : "\u25b8 ";
            if (GUILayout.Button(
                    arrow + (_advanced
                        ? PanelStrings.Override(_spec.BackText, PanelStrings.S("panel.back", "返回"))
                        : PanelStrings.Override(_spec.AdvancedText, PanelStrings.S("panel.advanced", "高级选项"))),
                    GUILayout.Height(30f), GUILayout.Width(160f)))
            {
                _advanced = !_advanced;
                _advancedScroll = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(PanelStrings.Override(_spec.CloseText, PanelStrings.S("panel.close_f7", "关闭  (F7)")),
                                 GUILayout.Height(30f)))
            {
                Open = false;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _rect.width, 24f));
        }

        // -- status ------------------------------------------------------------

        private void DrawStatus(ConfigBridge cfg)
        {
            if (_spec.Previewing)
            {
                GUILayout.Label(PanelStrings.S("panel.preview_test_data_for_checking_the_layou", "预览模式 —— 正在用测试数据检查布局，不会保存任何修改"), _styles.Headline);
            }

            GUILayout.Label(Headline(cfg), _styles.Headline);

            if (cfg.Names().Count == 0)
            {
                GUILayout.Label(PanelStrings.S("panel.nothing_to_block_yet", "还没有要冻结的应用。在下面选几个，之后一开始游戏计时它们就会被冻结。"), _styles.Hint);
            }
            else
            {
                GUILayout.Label(PanelStrings.S("panel.these_are_closed_while_the_game_s_timer_", "游戏计时运行期间（番茄钟或正向计时）这些应用会被冻结，计时结束后自动恢复。"), _styles.Hint);
            }

            GUILayout.Space(10f);
        }

        private string Headline(ConfigBridge cfg)
        {
            return HudState.PanelHeadline((key, chinese) => PanelStrings.S(key, chinese),
                                          _connected, _gateKnown, _gateEnabled, _gateSatisfied);
        }

        /// <summary>Everything the advanced page reports, gathered once per frame.</summary>
        private AdvancedStatus Status()
        {
            return new AdvancedStatus
            {
                Connected = _connected,
                Version = _focusedVersion,
                Url = _backendUrl,
                DryRun = _dryRun,
                GateKnown = _gateKnown,
                GateEnabled = _gateEnabled,
                FreezeKnown = _freezeKnown,
                FreezeEnabled = _freezeEnabled,
                FrozenCount = _frozenCount,
                FrozenNames = _frozenNames,
                ProcessError = _picker.ProcessError,
                LastRawError = _lastRawError,
                PluginVersion = FocusPluginInfo.FullVersion,
                GameLanguage = GameLanguageProbe.DetectedName ?? PanelText.Language.ToString(),
                GameLanguageFromGame = GameLanguageProbe.SupplierFound,
                ContainerFound = GameServices.ContainerFound,
            };
        }

        // -- helpers -----------------------------------------------------------

        private static void Report(string message)
        {
            var log = Log;
            if (log != null)
            {
                log(message);
            }
        }

        /// <summary>
        /// Parse a sample payload once at startup and report what came back.
        /// </summary>
        /// <remarks>
        /// The process picker depends on JsonUtility binding this response, and when
        /// it silently produced an empty list there was no way to tell whether the
        /// request, the parse, or the rendering was at fault. This makes that answer
        /// appear in the log on every launch.
        /// </remarks>
        internal static void SelfTest()
        {
            const string Sample =
                "{\"ok\":true,\"names\":[\"firefox\",\"wineserver\"],\"counts\":[3,1]," +
                "\"protected_flags\":[false,true],\"protect_rules\":[\"\",\"protect.name:wineserver\"]," +
                "\"processes\":[{\"name\":\"firefox\",\"count\":3,\"protected\":false," +
                "\"protect_rule\":\"\"},{\"name\":\"wineserver\",\"count\":1," +
                "\"protected\":true,\"protect_rule\":\"protect.name:wineserver\"}]}";

            try
            {
                var parsed = JsonUtility.FromJson<ProcessListResponse>(Sample);
                var names = parsed != null && parsed.names != null ? parsed.names.Length : -1;
                var items = parsed != null ? parsed.ToItems().Length : -1;

                Report("self-test: ok=" + (parsed != null && parsed.ok) +
                       " names=" + names + " items=" + items +
                       " protected[1]=" + (names > 1 && parsed.protected_flags[1]));
            }
            catch (Exception ex)
            {
                Report("self-test threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>Mark the gate as unknown, e.g. right after a disconnect.</summary>
        internal void ClearGate()
        {
            _gateKnown = false;
        }
    }
}
