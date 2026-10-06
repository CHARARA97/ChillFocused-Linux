using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Drives the mod from inside the game: hotkeys, polling, HUD and toasts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on a hidden <c>GameObject</c> so it survives scene changes. All Unity
    /// API use happens in <c>Update</c>/<c>OnGUI</c> (main thread); the network
    /// callbacks are only ever dequeued from <see cref="Update"/> via
    /// <see cref="FocusedClient.Pump"/>.
    /// </para>
    /// <para>
    /// The overlay is rendered with IMGUI rather than game UI. That costs some
    /// polish but keeps the mod free of any reference to the game's own
    /// assemblies, so a game update cannot break it.
    /// </para>
    /// </remarks>
    internal sealed class FocusRunner : MonoBehaviour
    {
        private const float BasePollFloor = 0.2f;
        private const float HudLineCacheSeconds = 0.25f;
        private const int MaxEventsPerPoll = 100;
        private const int MaxPumpActionsPerFrame = 16;

        private FocusSettings _settings = new FocusSettings();
        private FocusedClient _client;
        private ConfigBridge _config;
        private SettingsPanel _panel;
        private ConfigEntry<KeyboardShortcut> _toggleHud;
        private ConfigEntry<KeyboardShortcut> _toggleSettings;
        private Action<HudMode> _persistHud;
        private Func<FocusSettings> _settingsProvider;
        private Action _requestImmediateSync;
        private float _nextSettingsRefresh;
        private bool _prevSettingsPanel;

        // Panel layout, read from a local file and reloaded whenever it changes.
        private PanelSpec _panelSpec = PanelSpec.Defaults();
        private string _panelSpecPath = string.Empty;
        private string _panelTextPath = string.Empty;
        private DateTime _panelSpecStamp = DateTime.MinValue;
        private float _nextSpecCheck;

        // connection / backend state
        private bool _connected;
        private string _connectionError;
        private bool _focusedDryRun;
        private string _focusedVersion = "?";
        private string _lastWarning;
        private bool _gateEnabled;
        private bool _gateSatisfied;
        private bool _loggedGate;
        private bool _loggedVersionDiagnostics;
        private TimerState _timerState;

        // Mirrors the headless runner's decision, for the overlay and the panel.  This
        // class does not decide the session and does not report it: one owner per lease.
        private bool _sessionActive;

        // activity
        private int _closedTotal;
        private string[] _recentNames = new string[0];
        private string[] _frozenUnits = new string[0];
        private bool _freezeEnabled;
        private int _frozenCount;
        private string[] _frozenNames = new string[0];
        private bool _prevGateSatisfied;

        // polling state machine
        private float _nextPollAt;
        private readonly RuleSync _ruleSync = new RuleSync();
        private int _eventCursor = -1;

        // hotkey edge detection
        private bool _prevHud;

        // presentation
        private readonly Toasts _toasts = new Toasts();
        private readonly List<string> _hudLines = new List<string>();
        private float _hudLinesExpireAt;
        private HudRenderer _hud;

        /// <summary>
        /// The overlay's styles and drawing. Created on first use so that a runner
        /// without wiring still behaves exactly as it did before.
        /// </summary>
        private HudRenderer Hud
        {
            get { return _hud ?? (_hud = new HudRenderer(Inform, Warn)); }
        }

        // Diagnostics. Routed through BepInEx's logger by the plugin rather than
        // Debug.Log, because Unity log messages are not necessarily written to
        // BepInEx/LogOutput.log (that depends on [Logging.Disk] WriteUnityLog),
        // and a silent failure here is indistinguishable from "nothing happened".
        private bool _loggedFirstUpdate;
        private int _tickedFrame = -1;
        private bool _loggedFirstPoll;
        private bool _loggedFirstOnGui;
        private string _lastLoggedError;

        /// <summary>Diagnostics sink, wired to BepInEx's logger by the plugin.</summary>
        public Action<string> LogInfo;

        /// <summary>Diagnostics sink, wired to BepInEx's logger by the plugin.</summary>
        public Action<string> LogWarning;

        private void Inform(string message)
        {
            if (LogInfo != null)
            {
                LogInfo(message);
            }
            else
            {
                Debug.Log("[ChillFocused] " + message);
            }
        }

        private void Warn(string message)
        {
            if (LogWarning != null)
            {
                LogWarning(message);
            }
            else
            {
                Debug.LogWarning("[ChillFocused] " + message);
            }
        }

        public FocusSettings CurrentSettings
        {
            get { return _settings; }
        }

        public void Initialise(FocusRunnerWiring wiring)
        {
            if (wiring == null)
            {
                Inform("Initialise called without wiring; the overlay will not be created");
                return;
            }

            // Everything is created BEFORE anything is wired. Assigning to a field
            // that is still null throws from inside the plugin's Awake, and that
            // exception killed the entire overlay once already -- silently, because
            // it lands in Player.log rather than in the mod's own log.
            _panel = new SettingsPanel();
            SettingsPanel.Log = Inform;

            _toggleHud = wiring.ToggleHud;
            _toggleSettings = wiring.ToggleSettings;
            _persistHud = wiring.PersistHud;
            _config = wiring.Config;
            _settingsProvider = wiring.SettingsProvider;
            _requestImmediateSync = wiring.RequestImmediateSync;
            SetPanelSpecPath(wiring.PanelSpecPath);
            _panelTextPath = wiring.PanelTextPath;
            _client = wiring.Client;

            _panel.Toast = (message, warning) => AddToast(message, warning);
            _panel.SetGameGate = enabled =>
            {
                if (wiring.SetGameGate != null)
                {
                    wiring.SetGameGate(enabled);
                }
            };
            _panel.SetDryRun = dry =>
            {
                if (wiring.SetDryRun != null)
                {
                    wiring.SetDryRun(dry);
                }
            };
            _panel.ThawNow = () =>
            {
                if (wiring.ThawNow != null)
                {
                    wiring.ThawNow();
                }
                else
                {
                    _client.ThawAllAsync(null);
                }
            };
            _panel.ReloadSpec = ReloadPanelSpec;
            _panel.ScanNow = () =>
            {
                if (wiring.ScanNow != null)
                {
                    wiring.ScanNow();
                }
                else
                {
                    RequestScan();
                }
            };

            // Push a transition the moment it happens rather than waiting for the
            // background thread's next cycle.
            _timerState = GameTimerProbe.Snapshot();
            GameTimerProbe.Changed += OnTimerStateChanged;

            ApplySettings(wiring.Settings);
            AddToast(T("overlay.chillfocused_ready_f7_for_settings", "ChillFocused 已就绪 - F7 打开设置"), false);
        }

        /// <summary>Toast entry point for the plugin's advanced controls.</summary>
        public void ShowToast(string message, bool warning)
        {
            AddToast(message, warning);
        }

        private void SetWarning(string message)
        {
            _lastWarning = (message ?? string.Empty).Trim();

            // The raw text is for a bug report: the settings window's diagnostics
            // block shows it, the overlay never does.
            if (_panel != null)
            {
                _panel.SetRawError(_lastWarning);
            }

            if (_lastWarning.Length > 200)
            {
                _lastWarning = _lastWarning.Substring(0, 200);
            }
        }

        // -- toasts ----------------------------------------------------------

        private void AddToast(string text, bool warning)
        {
            Debug.Log("[ChillFocused] " + text);
            _toasts.Add(text, warning, Time.unscaledTime, _settings.ToastSeconds, Toasts.MaxEntries);
        }

        // -- presentation ----------------------------------------------------

        private void DrawHud()
        {
            if (Time.unscaledTime >= _hudLinesExpireAt)
            {
                _hudLinesExpireAt = Time.unscaledTime + HudLineCacheSeconds;
                BuildHudLines(_hudLines);
            }

            Hud.Draw(_panelSpec, _settings.Hud, _settings.HudCorner, _hudLines);
        }

        /// <summary>
        /// Rebuild the overlay's lines for the current mode.
        /// </summary>
        /// <remarks>
        /// Minimal is the default and is a single line. The only question the player
        /// usually has is whether the blacklist is being enforced, and burying that
        /// among eight rows of Focused detail was the worst possible answer.
        /// </remarks>
        private void BuildHudLines(List<string> lines)
        {
            lines.Clear();

            var enforcing = _connected && _sessionActive;

            // A filled circle for enforcing, hollow for not. Circles rather than an
            // emoji because the game's CJK font is guaranteed to have them.
            var dot = (enforcing ? "\u25cf " : "\u25cb ");
            var marker = enforcing ? string.Empty : "!";

            if (_settings.Hud == HudMode.Minimal)
            {
                var line = dot + HudState.StateReason(T, _connected, enforcing, _gateSatisfied);
                if (_closedTotal > 0)
                {
                    line += "        " + T("overlay.closed", "已冻结 ") + _closedTotal;
                }

                lines.Add(marker + line);
                return;
            }

            lines.Add(marker + dot + HudState.StateReason(T, _connected, enforcing, _gateSatisfied) +
                      (_focusedDryRun ? T("overlay.recording_only", "  [只记录]") : string.Empty));

            // No blank spacer line: the state style's own top/bottom padding controls
            // the gap, which is what lets the line sit centred between the title and
            // the detail rows instead of hugging one of them.

            var timerText = !_timerState.Known
                ? T("overlay.no_signal_yet", "尚无信号")
                : (_timerState.Working ? T("overlay.running", "运行中") : T("overlay.stopped", "已停止"));

            lines.Add(T("overlay.timer", "计时器") + "  ·  " + timerText);
            lines.Add(T("overlay.closed", "已冻结") + "  ·  " + _closedTotal);

            var frozen = HudState.RecentNames(_frozenNames, 3);
            if (frozen.Length > 0)
            {
                lines.Add(T("overlay.frozen", "已冻结") + "  ·  " + frozen);
            }

            var recent = HudState.RecentNames(_recentNames, 3);
            if (recent.Length > 0)
            {
                lines.Add(T("overlay.last_closed", "最近冻结") + "  ·  " + recent);
            }

            lines.Add(T("overlay.rules", "规则") + "  ·  " +
                      _settings.ProcessNames.Length + T("overlay.app_name_s", " 个应用名"));

            lines.Add(T("overlay.executor", "执行器") + "  ·  " +
                      (_connected
                          ? T("overlay.connected_v", "已连接 v") + _focusedVersion
                          : T("overlay.not_connected", "未连接")));

            lines.Add(T("overlay.hotkeys", "热键") + "  ·  " +
                      Key(_toggleHud) + " " + T("overlay.overlay", "面板") + "  ·  " +
                      Key(_toggleSettings) + " " + T("overlay.settings", "设置"));

            if (!string.IsNullOrEmpty(_lastWarning))
            {
                OverlayText.AddWarn(lines, T("overlay.last_error", "最近错误") + "  ·  "
                             + TextFit.Shorten(_lastWarning, 64));
            }
        }

        /// <summary>Draw the queued messages, oldest first, fading out near expiry.</summary>
        private void DrawToasts()
        {
            if (_toasts.Count == 0)
            {
                return;
            }

            Hud.EnsureStyles(_panelSpec);
            var y = 54f;
            for (var i = 0; i < _toasts.Count; i++)
            {
                var toast = _toasts.Items[i];
                var alpha = Toasts.Alpha(toast, Time.unscaledTime);
                var rect = new Rect(Screen.width * 0.5f - 230f, y, 460f, 26f);

                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(rect, Hud.PanelTexture);
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 18f),
                    toast.Text,
                    toast.Warning ? Hud.WarningStyle : Hud.LabelStyle);
                GUI.color = previous;
                y += 30f;
            }
        }

        private static string Key(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null || entry.Value.MainKey == KeyCode.None)
            {
                return "-";
            }

            return entry.Value.MainKey.ToString();
        }


        // -- Unity callbacks --------------------------------------------------

        private void Start()
        {
            Inform("runner Start() reached; the component is live in scene '" +
                   UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "'");
        }

        /// <summary>
        /// Self-driving per-frame work.
        /// </summary>
        /// <remarks>
        /// A runner created during the BepInEx chainloader never receives this
        /// message; one created later, once the game has settled, does. The plugin
        /// object itself is usually gone by then, so the runner cannot rely on being
        /// ticked from outside and drives itself.
        /// </remarks>
        private void Update()
        {
            Tick();
        }

        /// <summary>One frame of work. Safe to call from several sources.</summary>
        public void Tick()
        {
            if (_client == null || _tickedFrame == Time.frameCount)
            {
                return;
            }

            _tickedFrame = Time.frameCount;

            if (!_loggedFirstUpdate)
            {
                _loggedFirstUpdate = true;
                Inform("runner is ticking; executor " + _client.BaseUrl +
                       ", poll every " + _settings.PollIntervalSeconds.ToString("0.##") +
                       "s, overlay " + _settings.Hud);
            }

            // Follow the game's language setting; the probe returns immediately
            // unless its interval has elapsed.
            GameLanguageProbe.Tick(Inform, Warn);

            _client.Pump(MaxPumpActionsPerFrame);
            HandleHotkeys();
            _toasts.Expire(Time.unscaledTime);
            RefreshPanelSpec();

            // The panel and the .cfg file edit the same entries, so re-read them;
            // otherwise the overlay would show stale values.
            if (_settingsProvider != null && Time.unscaledTime >= _nextSettingsRefresh)
            {
                _nextSettingsRefresh = Time.unscaledTime + 1f;
                var fresh = _settingsProvider();
                if (fresh != null && !fresh.RulesEqual(_settings))
                {
                    _settings = fresh;
                    _hudLinesExpireAt = 0f;

                    // Adopting the new rules is only half the job: this path replaces the
                    // snapshot, which also makes the plugin's own sync compare equal and
                    // stay quiet. Without arming the push here, a rule change read this
                    // way would take effect on screen and never reach the executor.
                    _ruleSync.SettingsChanged();
                }
                else if (fresh != null)
                {
                    _settings.Hud = fresh.Hud;
                }
            }

            Poll();
        }

        private void OnGUI()
        {
            if (!_loggedFirstOnGui)
            {
                _loggedFirstOnGui = true;
                Inform("runner OnGUI is drawing the overlay");
            }

            Hud.EnsureStyles(_panelSpec);

            // Shown alongside the settings window rather than hidden by it: they
            // answer different questions, and having the live state next to the
            // controls is exactly what you want while changing something.
            if (_settings.Hud != HudMode.Off)
            {
                DrawHud();
            }

            if (_panel != null && _config != null)
            {
                _panel.Draw(_config, _client, Hud.PanelTexture);
            }

            DrawToasts();
        }

        private void OnDestroy()
        {
            GameTimerProbe.Changed -= OnTimerStateChanged;

            Inform("runner destroyed; the plugin will recreate it if the game is still running");

            if (_hud != null)
            {
                _hud.Destroy();
            }
        }

        // -- panel layout ------------------------------------------------------

        /// <summary>Point the runner at the layout file and force a first read.</summary>
        public void SetPanelSpecPath(string path)
        {
            _panelSpecPath = path ?? string.Empty;
            _panelSpecStamp = DateTime.MinValue;
            _nextSpecCheck = 0f;
        }

        /// <summary>Re-read the layout file now, whatever its timestamp says.</summary>
        public void ReloadPanelSpec()
        {
            _panelSpecStamp = DateTime.MinValue;
            _nextSpecCheck = 0f;
        }

        /// <summary>
        /// Pick up layout edits without restarting the game.
        /// </summary>
        /// <remarks>
        /// Checked once a second rather than every frame: the file is edited by a
        /// human, and a stat call per frame would be pure overhead.
        /// </remarks>
        private void RefreshPanelSpec()
        {
            if (Time.unscaledTime < _nextSpecCheck)
            {
                return;
            }

            _nextSpecCheck = Time.unscaledTime + 1f;
            if (string.IsNullOrEmpty(_panelSpecPath))
            {
                return;
            }

            var stamp = PanelSpecFile.Stamp(_panelSpecPath);
            if (stamp == _panelSpecStamp)
            {
                return;
            }

            _panelSpecStamp = stamp;

            var problems = new List<string>();
            _panelSpec = PanelSpecFile.Load(_panelSpecPath, problems);
            if (_panel != null)
            {
                _panel.SetSpec(_panelSpec);
            }

            // Force the overlay's own styles to rebuild against the new spec.
            if (_hud != null)
            {
                _hud.Reset();
            }

            for (var i = 0; i < problems.Count; i++)
            {
                Warn("panel layout: " + problems[i]);
            }

            var textProblems = new List<string>();
            PanelStrings.Text = PanelText.Load(_panelTextPath, textProblems);
            for (var i = 0; i < textProblems.Count; i++)
            {
                Warn("panel text: " + textProblems[i]);
            }

            // Say what the file is missing and which section each key belongs to. The
            // plugin never writes these files -- a mod that rewrites a player's config
            // destroys their edits -- so the log is how the gap gets closed.
            try
            {
                if (System.IO.File.Exists(_panelSpecPath))
                {
                    var missing = ConfigFile.Missing(System.IO.File.ReadAllText(_panelSpecPath),
                                                     PanelSpec.Template());
                    if (missing.Count > 0)
                    {
                        var text = new System.Text.StringBuilder();
                        foreach (var entry in missing)
                        {
                            if (text.Length > 0)
                            {
                                text.Append("; ");
                            }

                            text.Append(entry.Section.Length == 0 ? "未分组" : entry.Section)
                                .Append(": ").Append(entry.Key);
                        }

                        Inform("panel layout is missing " + missing.Count + " key(s) - " + text);
                    }
                }
            }
            catch (Exception ex)
            {
                Warn("could not compare the panel layout against the template: " + ex.Message);
            }

            Inform("panel layout loaded from " + _panelSpecPath +
                   " (" + problems.Count + " problem(s))" +
                   (_panelSpec.Previewing ? ", preview mode" : string.Empty));

            if (problems.Count == 0)
            {
                AddToast(T("overlay.panel_layout_reloaded", "面板布局已重新加载"), false);
            }
        }

        // -- polling -----------------------------------------------------------

        private void Poll()
        {
            if (_client == null || _client.Busy)
            {
                return;
            }

            var interval = Mathf.Max(BasePollFloor, _settings.PollIntervalSeconds);
            if (Time.unscaledTime < _nextPollAt)
            {
                return;
            }

            _nextPollAt = Time.unscaledTime + interval;

            if (_ruleSync.Due(Time.unscaledTime))
            {
                PushRules();
                return;
            }

            if (!_loggedFirstPoll)
            {
                _loggedFirstPoll = true;
                Inform("polling " + _client.BaseUrl + "/api/v1/focus");
            }

            _client.GetFocusStateAsync(delegate(FocusStateResponse status, string error)
            {
                if (error != null || status == null || !status.ok)
                {
                    MarkDisconnected(error ?? "unexpected response");
                    return;
                }

                var wasConnected = _connected;
                MarkConnected(status);

                if (!wasConnected)
                {
                    AddToast(T("overlay.connected_to_the_executor", "已连接到 Focused"), false);
                }

                Heartbeat(false);
                FetchEvents();
            });
        }

        /// <summary>
        /// Keep Focused's lease alive, and tell it the moment the session changes.
        /// </summary>
        /// <remarks>
        /// Sent on every change, and otherwise once per half-lease: frequent enough
        /// that a single lost request cannot let the lease lapse, cheap enough that it
        /// is one loopback request every few seconds.
        /// </remarks>
        /// <summary>Mirror the session decision the headless runner made.</summary>
        /// <remarks>
        /// This used to send its own heartbeat, gated on <c>_connected</c>.  Two runners
        /// therefore owned one lease, and because this one treats "not connected" as
        /// "not focusing" it released the session the other runner had just claimed --
        /// once every heartbeat, which is exactly what the flap in the log was.
        ///
        /// The session is a statement about the game, not about our HTTP client: if the
        /// connection drops, the report simply fails and Focused's lease expires, which
        /// is the fail-safe we want.  The headless runner owns the decision; this class
        /// reads it so the overlay and the panel agree.
        /// </remarks>
        private void Heartbeat(bool force)
        {
            _sessionActive = HeadlessRunner.SessionActive;
            _gateSatisfied = _sessionActive;
        }

        private void PushRules()
        {
            _ruleSync.Sent();
            _client.PushRulesAsync(_settings.ToRulePayload(),
                delegate(RulesResponse response, string error)
                {
                    if (error != null || response == null || !response.ok)
                    {
                        SetWarning("push rules failed: " + (error ?? "unexpected response"));
                        _ruleSync.Failed(Time.unscaledTime);
                        return;
                    }

                    Inform("rule set accepted by Focused");
                });
        }

        private void FetchEvents()
        {
            if (_client.Busy || _eventCursor < 0)
            {
                return;
            }

            _client.GetEventsAsync(_eventCursor, MaxEventsPerPoll,
                delegate(EventListResponse list, string error)
                {
                    if (error != null || list == null || !list.ok)
                    {
                        return;
                    }

                    _eventCursor = list.next_seq;
                });
        }

        private void MarkConnected(FocusStateResponse status)
        {
            if (!_connected)
            {
                _connected = true;
                _connectionError = null;
                _lastLoggedError = null;
                _eventCursor = 0;
            }

            _focusedDryRun = status.dry_run;
            _closedTotal = status.frozen_total;
            _recentNames = status.recent_names ?? new string[0];
            _sessionActive = status.active;

            // Freeze mode: what is suspended right now. Freezing is the only action
            // Focused has, so there is no "mode" switch to read back.
            var frozenBefore = _frozenCount;
            _freezeEnabled = true;
            _frozenCount = status.frozen_count;
            _frozenNames = status.frozen_names ?? new string[0];
            _frozenUnits = status.frozen_units ?? new string[0];
            _panel.SetFrozenState(true, _freezeEnabled, _frozenCount, FrozenNamesText());
            if (_frozenCount > frozenBefore && _frozenNames.Length > 0)
            {
                AddToast(T("overlay.frozen", "已冻结") + " " + _frozenNames[0], false);
            }
            else if (_frozenCount == 0 && frozenBefore > 0)
            {
                AddToast(T("toast.thaw", "恢复") + " " + T("panel.thaw_now", "立即恢复全部"), false);
            }

            // Flat fields only: JsonUtility does not bind nested objects, which is why
            // Focused's /focus payload is flat (see FocusStateResponse).
            var version = status.version;

            if (!string.IsNullOrEmpty(version))
            {
                _focusedVersion = version;
            }
            else if (!_loggedVersionDiagnostics)
            {
                _loggedVersionDiagnostics = true;
                Inform("status parse: no version in the response");
            }

            // Focused keeps the pushed rules in memory only, so a process that is not the
            // one we pushed to is a process that does not have them. Its pid is the signal:
            // it catches a restart between two successful polls, where no request ever
            // failed and nothing else would notice.
            _ruleSync.ObservedPid(status.pid);

            // The gate is decided here now, from the local switch and the game's own
            // timer reading, so it can be logged without asking anybody.
            if (!_loggedGate || _gateEnabled != !_settings.AlwaysOn || _gateSatisfied != _sessionActive)
            {
                _loggedGate = true;
                Inform("gate now: follow-timer=" + !_settings.AlwaysOn +
                       " timer=" + (_timerState.Working ? "working" : "idle") +
                       " session=" + _sessionActive);
            }

            _gateEnabled = !_settings.AlwaysOn;
            _gateSatisfied = _sessionActive;

            if (_panel != null)
            {
                _panel.SetGateState(true, _gateEnabled, _gateSatisfied);
                _panel.SetBackend(true, _focusedDryRun, _focusedVersion, _client.BaseUrl);
            }

            // Tell the player when a work session starts or ends. This is the
            // feedback that makes the whole thing feel automatic: the game's timer
            // is the only control there is.
            var enforcing = _connected && _sessionActive;
            if (enforcing != _prevGateSatisfied)
            {
                _prevGateSatisfied = enforcing;
                AddToast(
                    enforcing
                        ? T("overlay.work_session_blacklist_active", "开始创作 · 已冻结屏蔽名单应用")
                        : T("overlay.session_ended_blacklist_paused", "创作结束 · 已全部恢复"),
                    false);
                _hudLinesExpireAt = 0f;
            }
        }

        private void MarkDisconnected(string reason)
        {
            var wasConnected = _connected;
            _connected = false;
            _connectionError = reason;
            _eventCursor = -1;
            _prevGateSatisfied = false;

            if (_panel != null)
            {
                _panel.ClearGate();
                _panel.SetBackend(false, _focusedDryRun, _focusedVersion,
                                 _client != null ? _client.BaseUrl : string.Empty);
            }

            if (wasConnected || _lastLoggedError != reason)
            {
                _lastLoggedError = reason;
                SetWarning("Focused unreachable: " + reason);
                Warn("Focused unreachable: " + reason);
            }
        }

        // -- hotkeys -----------------------------------------------------------

        private void HandleHotkeys()
        {
            if (Hotkeys.Edge(Down(_toggleHud), ref _prevHud))
            {
                CycleHudMode();
            }

            if (Hotkeys.Edge(Down(_toggleSettings), ref _prevSettingsPanel) && _panel != null)
            {
                _panel.Open = !_panel.Open;
            }
        }

        /// <summary>Wording from the text file, falling back to the built-in string.</summary>
        private string T(string key, string chinese)
        {
            var text = PanelStrings.Text;
            return text != null ? text.Get(key, chinese) : chinese;
        }

        /// <summary>Whether a bound key is held right now; an unbound entry never is.</summary>
        private static bool Down(ConfigEntry<KeyboardShortcut> entry)
        {
            // KeyboardShortcut is a struct in BepInEx 5, so beyond the ConfigEntry
            // itself there is nothing to null-check.
            return entry != null && entry.Value.IsDown();
        }

        /// <summary>F9 walks the overlay through its three sizes.</summary>
        private void CycleHudMode()
        {
            var next = Hotkeys.NextHudMode(_settings.Hud);

            _settings.Hud = next;
            _hudLinesExpireAt = 0f;

            if (_persistHud != null)
            {
                _persistHud(next);
            }

            var notice = Hotkeys.HudModeNotice(next, T);
            if (notice != null)
            {
                AddToast(notice, false);
            }
        }

        // -- state -------------------------------------------------------------

        private void OnTimerStateChanged(TimerState state)
        {
            _timerState = state;
            _hudLinesExpireAt = 0f;

            // Waking the sync thread rather than posting from here: its transport is
            // its own, so this cannot be dropped with "another request is already in
            // flight", which is exactly what happened when the transition landed on
            // top of the overlay's poll.
            if (_requestImmediateSync != null)
            {
                _requestImmediateSync();
            }
        }

        public void ApplySettings(FocusSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            var rulesChanged = !_settings.RulesEqual(settings);
            _settings = settings;

            if (rulesChanged)
            {
                // Push promptly; this is also what applies a config edit live.
                _ruleSync.SettingsChanged();
            }

            _hudLinesExpireAt = 0f;
        }

        public void SetClient(FocusedClient client)
        {
            _client = client;
            _eventCursor = -1;
            _loggedFirstPoll = false;
            _nextPollAt = 0f;
        }

        private string FrozenNamesText()
        {
            return HudState.RecentNames(_frozenNames, 3);
        }

        private void RequestScan()
        {
            if (_client == null)
            {
                return;
            }

            _client.ForceScanAsync(delegate(ScanResponse response, string error)
            {
                if (error != null || response == null || !response.ok)
                {
                    SetWarning("scan failed: " + (error ?? "unexpected response"));
                }
            });
        }
    }

    /// <summary>Everything the runner needs, handed over in one go.</summary>
    internal sealed class FocusRunnerWiring
    {
        public FocusSettings Settings;
        public FocusedClient Client;
        public ConfigBridge Config;
        public ConfigEntry<KeyboardShortcut> ToggleHud;
        public ConfigEntry<KeyboardShortcut> ToggleSettings;
        public Action<HudMode> PersistHud;
        public Func<FocusSettings> SettingsProvider;

        /// <summary>Nudge the background sync thread to report at once.</summary>
        public Action RequestImmediateSync;

        /// <summary>Panel layout file, hot-reloaded by the runner.</summary>
        public string PanelSpecPath;

        /// <summary>Panel wording file, hot-reloaded alongside it.</summary>
        public string PanelTextPath;

        /// <summary>Surface a short message on the overlay.</summary>
        public Action<string, bool> Toast;

        /// <summary>Ask Focused to follow the game timer, or to ignore it.</summary>
        public Action<bool> SetGameGate;

        /// <summary>Toggle dry-run (record only) on Focused.</summary>
        public Action<bool> SetDryRun;

        /// <summary>Resume everything Focused suspended, right now.</summary>
        public Action ThawNow;

        /// <summary>Scan immediately instead of waiting for the next tick.</summary>
        public Action ScanNow;
    }

}
