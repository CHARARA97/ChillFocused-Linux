using System;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using ChillFocused.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChillFocused
{
    /// <summary>
    /// BepInEx entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This plugin deliberately references <b>no</b> game assembly. It never
    /// patches game code, so it cannot be broken by a game update that moves
    /// methods around; the only thing it needs from Unity is the ability to run
    /// a MonoBehaviour and draw an IMGUI overlay.
    /// </para>
    /// <para>
    /// Everything that can actually terminate a process lives in the host-side
    /// <c>Focused</c>. That split is not a design preference: a plugin inside
    /// a Proton/Wine prefix cannot see or signal native Linux processes at all.
    /// </para>
    /// </remarks>
    [BepInPlugin(FocusPluginInfo.Guid, FocusPluginInfo.Name, FocusPluginInfo.Version)]
    public sealed class ChillFocusedPlugin : BaseUnityPlugin
    {
        private const string SectionGeneral = "1. General";
        private const string SectionBlacklist = "2. Blacklist";
        private const string SectionProtect = "3. Protect";
        private const string SectionOverlay = "4. Overlay";
        private const string SectionHotkeys = "5. Hotkeys";

        /// <summary>How often the configuration file is re-checked for edits.</summary>
        private const float ConfigCheckSeconds = 0.25f;

        private ConfigEntry<bool> _runInBackground;
        private ConfigEntry<string> _focusedUrl;
        private ConfigEntry<string> _focusedToken;
        private ConfigEntry<bool> _pluginEnabled;
        private ConfigEntry<bool> _alwaysOn;
        private ConfigEntry<bool> _dryRun;
        private ConfigEntry<float> _timerFreshSeconds;
        private ConfigEntry<int> _sessionLeaseSeconds;
        private ConfigEntry<int> _requestTimeoutMs;
        private ConfigEntry<float> _pollInterval;

        private ConfigEntry<string> _processNames;
        private ConfigEntry<string> _cmdlineSubstrings;
        private ConfigEntry<string> _protectedNames;
        private ConfigEntry<string> _protectedCmdlineSubstrings;
        private ConfigEntry<HudMode> _hudMode;
        private ConfigEntry<string> _hudCorner;
        private ConfigEntry<float> _toastSeconds;
        private ConfigEntry<KeyboardShortcut> _toggleHud;
        private ConfigEntry<KeyboardShortcut> _toggleSettings;
        private ConfigBridge _bridge;

        private FocusRunner _runner;
        private HeadlessRunner _headless;
        private string _lastConfigFingerprint;
        private float _nextConfigCheckAt;
        private bool _loggedFirstTick;
        private float _nextHeartbeatAt;
        private int _heartbeatsLeft = 12;
        private int _tickedFrame = -1;
        private float _nextStateLogAt;
        private string _lastKnownState;
        private readonly object _configGate = new object();
        private DateTime _configStampUtc = DateTime.MinValue;
        private string _configPath;

        /// <summary>Panel layout file, hot-reloaded by the runner while the game runs.</summary>
        private string _panelSpecPath;
        private string _panelTextPath;
        private SynchronizationContext _unityContext;
        private int _mainPosts;
        private int _mainRuns;
        private bool _loggedMainRun;

        private void Awake()
        {
            BindConfig();

            // Log the focus state before anything else can influence it: if the
            // frame loop never ticks, this line is the only evidence about why.
            Logger.LogInfo(
                "focus state at Awake: isFocused=" + Application.isFocused +
                ", runInBackground=" + Application.runInBackground);

            if (_runInBackground.Value && !Application.runInBackground)
            {
                Application.runInBackground = true;
                Logger.LogInfo("set Application.runInBackground = true to keep the frame loop alive");
            }

            InstallFrameHook();
            InstallGameProbe();
            InstallMainThreadPump();

            // Recreate the overlay object once a scene has actually finished
            // loading. Objects created while the chainloader runs are created
            // before the first scene exists, so DontDestroyOnLoad does not take
            // effect and they are destroyed almost immediately.
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                Logger.LogInfo("subscribed to SceneManager.sceneLoaded");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not subscribe to SceneManager.sceneLoaded: " + ex.Message);
            }

            // Kept as an extra source in case some future render path does drive
            // it; it costs nothing and the tick scheduler dedupes by frame.
            try
            {
                Application.onBeforeRender += OnBeforeRenderTick;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not subscribe to Application.onBeforeRender: " + ex.Message);
            }

            try
            {
                _configPath = Config != null ? Config.ConfigFilePath : null;
            }
            catch (Exception)
            {
                _configPath = null;
            }

            _bridge = new ConfigBridge(_hudMode, _processNames, _cmdlineSubstrings, _protectedNames);

            // Read the game's timer state from the game's own services. This is what
            // makes a session start or stop take effect immediately instead of after
            // Focused's save-file window.
            GameTimerProbe.Install(message => Logger.LogInfo(message));

            // Parsing a baked-in sample at startup: the process picker depends on
            // JsonUtility binding the response, and if it ever stops doing so this
            // says so on the very first launch rather than leaving an empty list.
            SettingsPanel.Log = message => Logger.LogInfo("[panel] " + message);
            SettingsPanel.SelfTest();
            _configStampUtc = ConfigStampUtc();

            // Written next to the plugin's own config so it is easy to find, and
            // created on first run with every key and range documented: a layout
            // file nobody knows about is not a debugging aid.
            _panelSpecPath = PanelSpecFile.DefaultPath(_configPath);
            _panelTextPath = Path.Combine(
                Path.GetDirectoryName(_panelSpecPath) ?? ".", "com.chillfocused.text.cfg");
            if (!File.Exists(_panelTextPath))
            {
                try
                {
                    File.WriteAllText(_panelTextPath, new PanelText().Template());
                    Logger.LogInfo("wrote a panel text template to " + _panelTextPath);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("cannot write the panel text file: " + ex.Message);
                }
            }
            Logger.LogInfo(PanelSpecFile.EnsureExists(_panelSpecPath)
                ? "wrote a panel layout template to " + _panelSpecPath
                : "panel layout file: " + _panelSpecPath);

            var settings = ReadSettings();
            LogSettingsProblems(settings);
            _lastConfigFingerprint = ConfigFingerprint();

            CreateRunner(settings);

            // The sync thread is the part that must work unconditionally, so it
            // is started last and independently of anything Unity-related above.
            StartHeadlessSync(settings);

            Logger.LogInfo(
                FocusPluginInfo.Name + " " + FocusPluginInfo.FullVersion + " loaded from " +
                (Info != null && !string.IsNullOrEmpty(Info.Location) ? Info.Location : "?") +
                "; backend at " + _focusedUrl.Value + ".");
            Logger.LogInfo(
                "the session decision follows the game's own timer and is handed to Focused as a " +
                "lease; the suspending itself happens outside the game.");
            Logger.LogInfo("panel language: starting as " + PanelText.Language +
                           "; it follows the game's setting once the probe reads it " +
                           "(override any string in com.chillfocused.text.cfg)");
            ReportForeignPluginCopies();
        }

        /// <summary>
        /// Warn when a second copy of this plugin sits in the plugin folder.
        /// </summary>
        /// <remarks>
        /// BepInEx loads every DLL under <c>plugins/</c>, subdirectories included, so
        /// an upgrade that forgot to delete the previous file runs the mod twice.
        /// Nothing throws; the user just gets two overlays and two poll loops.  The
        /// listing is best-effort: if it cannot be read, the plugin carries on.
        /// </remarks>
        private void ReportForeignPluginCopies()
        {
            try
            {
                var pluginRoot = Paths.PluginPath;
                if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                {
                    return;
                }

                var self = Info != null ? Info.Location : null;
                var dlls = Directory.GetFiles(pluginRoot, "*.dll", SearchOption.AllDirectories);
                var foreign = PluginInstallCheck.ForeignCopies(dlls, self);
                var warning = PluginInstallCheck.Warning(foreign, self);
                if (warning != null)
                {
                    Logger.LogWarning(warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("install check skipped: " + ex.Message);
            }
        }

        /// <summary>
        /// Reconcile the runner with the configuration file.
        /// </summary>
        /// <remarks>
        /// BepInEx 5 exposes <c>SettingChanged</c> on the generic entry type, not
        /// on <c>ConfigEntryBase</c>, so subscribing uniformly across every entry
        /// would need a dozen near-identical handlers. Polling a cheap
        /// fingerprint on a 0.25s timer instead costs nothing (config edits are
        /// not latency-sensitive), allocates far less than a per-frame check, and
        /// cannot miss an edit made by editing the file by hand while the game
        /// runs -- which event-based tracking would.
        /// </remarks>
        /// <summary>
        /// Patch a method the player calls every frame, so the mod keeps working
        /// even if every GameObject it creates is destroyed.
        /// </summary>
        private void InstallFrameHook()
        {
            FrameHook.Tick = () => Tick("Canvas.SendWillRenderCanvases");

            try
            {
                var harmony = new Harmony(FocusPluginInfo.Guid + ".framehook");
                var target = AccessTools.Method(typeof(Canvas), "SendWillRenderCanvases");
                if (target == null)
                {
                    Logger.LogWarning(
                        "Canvas.SendWillRenderCanvases was not found; falling back to Update only");
                    return;
                }

                harmony.Patch(
                    target,
                    postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(FrameHook), "Postfix")));
                Logger.LogInfo("installed the per-frame hook on " + target.FullDescription());
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not install the per-frame hook: " + ex);
            }
        }

        /// <summary>
        /// Patch the game's own methods so the mod can (a) act on the main thread
        /// after the game has settled and (b) observe which mode the game is in.
        /// </summary>
        private void InstallGameProbe()
        {
            try
            {
                var targets = GameProbe.Install(
                    message => Logger.LogInfo(message),
                    message => Logger.LogWarning(message));

                if (targets == 0)
                {
                    Logger.LogWarning(
                        "no game hook could be installed; mode detection and the deferred " +
                        "overlay object will not work");
                    return;
                }

                var description = GameProbe.Describe();
                GameProbe.Tick = () => Tick("GameProbe:" + description);
                Logger.LogInfo("game probe active on: " + description);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not install the game probe: " + ex);
            }
        }

        /// <summary>
        /// Capture Unity's main-thread SynchronizationContext.
        /// </summary>
        /// <remarks>
        /// Unity pumps this queue from its player loop every frame, so posting to
        /// it is a way to run code on the main thread that needs no GameObject and
        /// therefore cannot be taken away by whatever destroys the objects created
        /// during the chainloader. That makes it the right place to create the
        /// overlay *late*, once a scene is definitely live.
        /// </remarks>
        private void InstallMainThreadPump()
        {
            try
            {
                _unityContext = SynchronizationContext.Current;
                Logger.LogInfo(
                    "main thread id=" + Thread.CurrentThread.ManagedThreadId +
                    ", synchronization context=" +
                    (_unityContext == null ? "null" : _unityContext.GetType().FullName));
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not capture the main-thread context: " + ex.Message);
                return;
            }

            if (_unityContext == null)
            {
                Logger.LogWarning("no synchronization context; the late overlay creation cannot work");
                return;
            }

            PostToMainThread(() =>
            {
                Logger.LogInfo("main-thread pump is alive (first posted callback ran)");

                // One-shot inventory of the game's API, filtered to the parts
                // that could plausibly carry "which mode is the player in".
                GameProbe.DumpApi(
                    message => Logger.LogInfo("[api] " + message),
                    new[]
                    {
                        "Pomodoro", "RoomGame", "GameMode", "MainState", "Study",
                        "Work", "Timer", "ScenarioProgress", "PageData", "Collaboration",
                    },
                    40,
                    14);
            });
        }

        /// <summary>Queue work onto Unity's main thread without needing a GameObject.</summary>
        private void PostToMainThread(Action work)
        {
            var context = _unityContext;
            if (context == null || work == null)
            {
                return;
            }

            Interlocked.Increment(ref _mainPosts);
            try
            {
                context.Post(
                    _ =>
                    {
                        Interlocked.Increment(ref _mainRuns);
                        if (!_loggedMainRun)
                        {
                            _loggedMainRun = true;
                            Logger.LogInfo("first main-thread callback executed (posts=" +
                                           Volatile.Read(ref _mainPosts) + ")");
                        }

                        try
                        {
                            work();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning("main-thread work failed: " + ex.Message);
                        }
                    },
                    null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not post to the main thread: " + ex.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo("scene loaded: '" + scene.name + "' (" + mode + ")");
            EnsureRunner();
        }

        private void OnBeforeRenderTick()
        {
            Tick("Application.onBeforeRender");
        }

        private void Update()
        {
            Tick("Update");
        }

        /// <summary>
        /// The single frame scheduler for the whole plugin.
        /// </summary>
        /// <remarks>
        /// Several sources can call this (Unity's Update on the plugin component,
        /// the static onBeforeRender event). The frame stamp makes the first one
        /// win and the rest no-ops, so per-frame work happens exactly once
        /// without caring which sources survive.
        /// </remarks>
        private void Tick(string source)
        {
            if (_tickedFrame == Time.frameCount)
            {
                return;
            }

            _tickedFrame = Time.frameCount;

            if (!_loggedFirstTick)
            {
                _loggedFirstTick = true;
                Logger.LogInfo("first tick arrived via " + source + " (frame " + Time.frameCount + ")");
            }

            // A short heartbeat makes "is Unity still ticking, and does it stop
            // when the window loses focus?" answerable from the log alone.
            if (_heartbeatsLeft > 0 && Time.unscaledTime >= _nextHeartbeatAt)
            {
                _heartbeatsLeft--;
                _nextHeartbeatAt = Time.unscaledTime + 5f;
                Logger.LogInfo(
                    "heartbeat via " + source + ": frame=" + Time.frameCount +
                    " focused=" + Application.isFocused +
                    " runnerAlive=" + (_runner != null) +
                    " mainPosts=" + Volatile.Read(ref _mainPosts) +
                    " mainRuns=" + Volatile.Read(ref _mainRuns));
            }

            EnsureRunner();
            if (_runner != null)
            {
                _runner.Tick();
            }

            // Report the game's own mode whenever it changes. This is the data
            // needed to decide which mode the user means by "creative mode".
            if (Time.unscaledTime >= _nextStateLogAt)
            {
                _nextStateLogAt = Time.unscaledTime + 1f;
                var state = GameProbe.ReadGameState();
                if (state != _lastKnownState)
                {
                    _lastKnownState = state;
                    Logger.LogInfo("game mode changed: CurrentMainState=" +
                                   (state ?? "(unreadable)"));
                }
            }

            if (Time.unscaledTime < _nextConfigCheckAt)
            {
                return;
            }

            _nextConfigCheckAt = Time.unscaledTime + ConfigCheckSeconds;

            lock (_configGate)
            {
                ReloadConfigIfChanged();
            }

            var fingerprint = ConfigFingerprint();
            if (fingerprint == _lastConfigFingerprint)
            {
                return;
            }

            _lastConfigFingerprint = fingerprint;
            SyncSettings();
        }

        /// <summary>
        /// Create the runner object and wire it up.
        /// </summary>
        private void CreateRunner(FocusSettings settings)
        {
            var host = new GameObject("ChillFocusedRunner");
            // HideInHierarchy only: purely cosmetic. HideAndDontSave also carries
            // asset-lifetime flags, and nothing about the runner should depend on
            // those.
            host.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(host);
            _runner = host.AddComponent<FocusRunner>();

            // Wire diagnostics to BepInEx's logger before Initialise so even the
            // first tick is visible in LogOutput.log.
            _runner.LogInfo = message => Logger.LogInfo(message);
            _runner.LogWarning = message => Logger.LogWarning(message);
            var wiring = new FocusRunnerWiring
            {
                Settings = settings,
                Client = BuildClient(settings),
                Config = _bridge,
                ToggleHud = _toggleHud,
                ToggleSettings = _toggleSettings,
                SetGameGate = SetGameGate,
                SetDryRun = SetDryRun,
                ThawNow = ThawNow,
                ScanNow = ScanNow,
                Toast = (message, warning) => RunnerToast(message, warning),
                PersistHud = PersistHudMode,
                SettingsProvider = ReadSettingsFresh,
                RequestImmediateSync = () => _headless.Wake(),
                PanelSpecPath = _panelSpecPath,
                PanelTextPath = _panelTextPath,
            };

            // A throw here used to remove the overlay entirely and leave only a
            // line in Player.log. Failing loudly but surviving is strictly better:
            // the component stays alive and the reason is in the mod's own log.
            try
            {
                _runner.Initialise(wiring);
            }
            catch (Exception ex)
            {
                Logger.LogError("could not initialise the overlay: " +
                                ex.GetType().Name + ": " + ex.Message +
                                "\n" + ex.StackTrace);
            }

            if (_lastConfigFingerprint == null)
            {
                _lastConfigFingerprint = ConfigFingerprint();
            }
        }

        /// <summary>
        /// Recreate the runner if it has gone away.
        /// </summary>
        /// <remarks>
        /// BepInEx runs the plugin's Awake while the game's first scene is still
        /// loading. An object created there can be destroyed by a subsequent
        /// scene transition, which silently kills every Update, OnGUI and hotkey
        /// with no exception to show for it. Rather than depend on
        /// DontDestroyOnLoad behaving, the runner is simply recreated when it is
        /// missing. Unity's overloaded null check reports destroyed objects as
        /// null, so this is also safe against a destroyed component.
        /// </remarks>
        private void EnsureRunner()
        {
            if (_runner != null)
            {
                return;
            }

            Logger.LogWarning("FocusRunner is missing (destroyed or never created); recreating it");
            CreateRunner(ReadSettings());
        }

        /// <summary>
        /// Start the Unity-independent sync loop.
        /// </summary>
        /// <remarks>
        /// This exists because on this game under Proton nothing created during
        /// the BepInEx chainloader ever receives a frame callback. The switch and
        /// the blacklist are read back from the BepInEx config on every cycle, so
        /// editing the .cfg file is enough to arm or disarm the mod even if the
        /// overlay never appears.
        /// </remarks>
        private void StartHeadlessSync(FocusSettings settings)
        {
            try
            {
                _headless = new HeadlessRunner(
                    ReadSettingsFresh,
                    settings.FocusedUrl,
                    settings.FocusedToken,
                    settings.RequestTimeoutMs,
                    2.0,
                    message => Logger.LogInfo("[sync] " + message),
                    message => Logger.LogWarning("[sync] " + message));
                _headless.OnCycle = () => PostToMainThread(EnsureRunner);
                _headless.GameStateProvider = GameTimerProbe.Snapshot;
                _headless.Start();
            }
            catch (Exception ex)
            {
                Logger.LogError("could not start the sync thread: " + ex);
            }
        }

        private DateTime ConfigStampUtc()
        {
            try
            {
                return string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath)
                    ? DateTime.MinValue
                    : File.GetLastWriteTimeUtc(_configPath);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>Explicit reload, used when there is no running Update loop.</summary>
        private void ReloadConfigIfChanged()
        {
            try
            {
                if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
                {
                    return;
                }

                var stamp = File.GetLastWriteTimeUtc(_configPath);
                if (stamp == _configStampUtc)
                {
                    return;
                }

                Config.Reload();

                // Reload() writes the file back out, which changes its mtime.
                // Re-stamp afterwards or this check would trigger itself forever.
                _configStampUtc = ConfigStampUtc();
                Logger.LogInfo("config file changed on disk; reloaded it");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("could not reload the config file: " + ex.Message);
            }
        }

        /// <summary>
        /// Read the settings, reloading the config file first if it changed.
        /// </summary>
        /// <remarks>
        /// BepInEx does install a file watcher, but it did not fire for this file
        /// under Proton, so edits made while the game was running were never
        /// seen. Since the config file is the only control surface this mod has
        /// in this game (the game destroys the plugin object before any frame
        /// callback arrives, so there is no overlay or hotkey), picking up edits
        /// promptly is the whole user interface. The mtime check makes it cheap
        /// enough to run every cycle.
        /// </remarks>
        private FocusSettings ReadSettingsFresh()
        {
            lock (_configGate)
            {
                ReloadConfigIfChanged();
                return ReadSettings();
            }
        }

        /// <summary>True once a per-frame source has been seen.</summary>
        public bool HasTicked
        {
            get { return _loggedFirstTick; }
        }

        /// <summary>
        /// Unity calls this when it believes focus changed. It is a useful second
        /// signal: if the frame loop is paused but this still fires, focus
        /// detection works and something else is stopping Update.
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            Logger.LogInfo("OnApplicationFocus(" + focused + ")");
        }

        private void OnApplicationPause(bool paused)
        {
            Logger.LogInfo("OnApplicationPause(" + paused + ")");
        }

        private void OnDestroy()
        {
            // Diagnostic: if this appears while the game is still running, the
            // game destroyed the plugin's own GameObject, which is why no Unity
            // message ever reaches this plugin afterwards.
            Logger.LogInfo("plugin OnDestroy: the game is destroying the plugin object" +
                           (HasTicked ? " (after " + _tickedFrame + " frames)" : " (never ticked)"));

            // Deliberately NOT stopping the sync thread here. In this game the
            // plugin's own GameObject is destroyed moments after Awake, so
            // OnDestroy fires while the game is still running -- stopping the
            // thread then would leave Focused stuck with whatever state it
            // happened to have and unable to follow later config edits. The
            // thread is the one thing that must outlive the object; it is a
            // background thread and dies with the process.

            try
            {
                Application.onBeforeRender -= OnBeforeRenderTick;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                FrameHook.Tick = null;
                GameProbe.Tick = null;
            }
            catch (Exception)
            {
                // shutting down; nothing useful to do
            }

            if (_runner != null)
            {
                Destroy(_runner.gameObject);
            }
        }

        // -- configuration ----------------------------------------------------

        private void BindConfig()
        {
            _runInBackground = Config.Bind(
                SectionGeneral, "RunInBackground", true,
                "窗口失焦时仍保持插件运行（默认开启）。\n" +
                "某些 Wayland/XWayland 组合下合成器不发布 _NET_ACTIVE_WINDOW，Unity\n" +
                "无法判断自己是否获得焦点，于是把整个帧循环暂停：Update/OnGUI 一次都\n" +
                "不调用，表现为「没有面板、也不与守护进程通信」，而且不报任何异常。\n" +
                "开启此项会设置 Application.runInBackground = true 来规避。\n" +
                "Keep the plugin ticking while the window is not focused. Some Wayland/\n" +
                "XWayland compositors publish no _NET_ACTIVE_WINDOW, so Unity cannot tell\n" +
                "whether it is focused and pauses the frame loop entirely: no Update, no\n" +
                "OnGUI, no exception. Sets Application.runInBackground = true.");

            _focusedUrl = Config.Bind(
                SectionGeneral, "FocusedUrl", "http://127.0.0.1:8766",
                "Focused 应用的 HTTP 地址。必须是回环地址：该 API 可以挂起进程。\n" +
                "Base URL of the Focused application. Must be loopback: the API can\n" +
                "suspend processes. The game runs in a Wine prefix but shares the host\n" +
                "network stack, so 127.0.0.1 reaches Focused.");

            _focusedToken = Config.Bind(
                SectionGeneral, "FocusedToken", string.Empty,
                "可选共享令牌，需与 Focused config.json 的 http.token 一致。\n" +
                "Optional shared secret. When set it must match 'http.token' in\n" +
                "Focused's config.json, and is sent as the X-Focused-Token header.");

            _pluginEnabled = Config.Bind(
                SectionGeneral, "Enabled", true,
                "总开关。关闭后永远不会冻结任何应用（只在面板/热键里改也行）。\n" +
                "Master switch. When off, nothing is ever suspended.");

            _alwaysOn = Config.Bind(
                SectionGeneral, "AlwaysOn", false,
                "true = 不看游戏计时器，游戏运行时一直生效；false = 跟随游戏计时器。\n" +
                "true = ignore the game's timer and stay active while the game runs;\n" +
                "false = follow the game's own work sessions.");

            _dryRun = Config.Bind(
                SectionGeneral, "DryRun", false,
                "演练模式：只记录会冻结谁，不真的冻结。\n" +
                "Rehearsal: record what would be suspended, suspend nothing.");

            _timerFreshSeconds = Config.Bind(
                SectionGeneral, "TimerFreshSeconds", 25f,
                "游戏计时器读数多久算新鲜（秒）。超时视为这次创作结束。\n" +
                "How long the game's last timer reading stays believable. After that\n" +
                "the work session is considered over and everything is resumed.");

            _sessionLeaseSeconds = Config.Bind(
                SectionGeneral, "SessionLeaseSeconds", 30,
                "会话租约（秒）。插件会持续续期；一旦插件停止（崩溃/被杀/被冻），\n" +
                "Focused 会在租约到期后自动恢复所有被挂起的应用。\n" +
                "Session lease in seconds, renewed while a session runs. If this\n" +
                "plugin stops existing, Focused resumes everything when it lapses.");

            _requestTimeoutMs = Config.Bind(
                SectionGeneral, "RequestTimeoutMs", 1500,
                "单次 HTTP 请求超时（毫秒）。请求在后台线程执行，不会卡住游戏。\n" +
                "Per-request timeout in milliseconds. Requests run on a background\n" +
                "thread, so this bounds latency, not frame rate.");

            _pollInterval = Config.Bind(
                SectionGeneral, "PollIntervalSeconds", 1.0f,
                "轮询守护进程状态的间隔（秒），下限 0.2。\n" +
                "How often to poll Focused, in seconds. Floor of 0.2s; a faster\n" +
                "poll buys nothing because Focused's own scan interval is >= 0.2s.");

            _processNames = Config.Bind(
                SectionBlacklist, "ProcessNames", string.Empty,
                "要冻结的进程名，用分号或换行分隔，支持 * 和 ? 通配符，大小写不敏感。\n" +
                "例：firefox; chromium*; discord; Slack\n" +
                "Process names to terminate, separated by ';' or newlines. Glob '*'\n" +
                "and '?' are supported; matching is case-insensitive. Both the kernel\n" +
                "task name (comm) and the executable basename are considered.\n" +
                "NOTE: names longer than 15 characters also match their kernel-\n" +
                "truncated form, e.g. 'chromium-browser' matches comm 'chromium-browse'.");

            _cmdlineSubstrings = Config.Bind(
                SectionBlacklist, "CmdlineSubstrings", string.Empty,
                "要冻结的命令行子串（用于改名或由脚本启动的应用），分号/换行分隔。\n" +
                "例：--profile distractor; /opt/tools/distraction\n" +
                "Substrings matched against the target's full command line. Use this\n" +
                "when a process can be renamed, or when only one of several instances\n" +
                "should be blocked. Commas are NOT separators, so '--flag=a,b' is safe.");

            _protectedNames = Config.Bind(
                SectionProtect, "ExtraProtectedNames", string.Empty,
                "额外的白名单进程名。白名单只增不减：这里的内容会与守护进程内置的\n" +
                "保护名单（wineserver、steam、桌面合成器、systemd 等）合并。\n" +
                "Extra protect names, separated by ';' or newlines. This list is\n" +
                "ADDITIVE: Focused keeps its built-in rails (wineserver, steam, the\n" +
                "compositor, systemd, ...) and unions them with these. Protection always\n" +
                "wins over the blacklist, so an entry here can never be killed.");

            _protectedCmdlineSubstrings = Config.Bind(
                SectionProtect, "ExtraProtectedCmdlineSubstrings", string.Empty,
                "额外的白名单命令行子串，同样只增不减。\n" +
                "Extra protect command-line substrings. Also additive.");

            _hudMode = Config.Bind(
                SectionOverlay, "HudMode", HudMode.Minimal,
                "游戏内状态面板显示多少内容。F9 循环切换。\n" +
                "  Minimal  = 一行：●/○ 状态 + 已冻结数量（默认）\n" +
                "  Detailed = 分组多行，含计时器/执行器/热键，排查时用\n" +
                "  Off      = 完全不显示\n" +
                "How much the in-game overlay shows. F9 cycles it. Minimal is one line:\n" +
                "a filled circle while the blacklist is being enforced, how many were\n" +
                "closed, and nothing else. Detailed adds the timer, executor and hotkey\n" +
                "rows for troubleshooting.");

            _hudCorner = Config.Bind(
                SectionOverlay, "HudCorner", "top-left",
                "面板位置：top-left / top-right / bottom-left / bottom-right。\n" +
                "Overlay corner: top-left, top-right, bottom-left or bottom-right.");

            _toastSeconds = Config.Bind(
                SectionOverlay, "ToastSeconds", 4.0f,
                "冻结提示的显示时长（秒）。\n" +
                "How long an interception notice stays on screen, in seconds.");

            _toggleHud = Config.Bind(
                SectionHotkeys, "ToggleHud", new KeyboardShortcut(KeyCode.F9),
                "显示/隐藏状态面板。\n" +
                "Show or hide the status overlay.");

            _toggleSettings = Config.Bind(
                SectionHotkeys, "ToggleSettings", new KeyboardShortcut(KeyCode.F7),
                "打开/关闭游戏内设置面板。\n" +
                "Open or close the in-game settings window.");

        }

        /// <summary>
        /// Follow the game's timer, or ignore it entirely.
        /// </summary>
        /// <remarks>
        /// This is the single behavioural switch left. With it off, enforcement is
        /// unconditional, which is also what makes Focused usable with no game
        /// running -- so Focused's own master switch has to be armed for it.
        /// </remarks>
        private void SetGameGate(bool followTimer)
        {
            // Local, and persisted: the plugin owns this decision now, and the next
            // heartbeat carries the answer to Focused.
            _alwaysOn.Value = !followTimer;
            if (!followTimer)
            {
                _pluginEnabled.Value = true;
            }

            ApplySettingsNow();
            Logger.LogInfo("game gate set to " +
                           (followTimer ? "follow the game timer" : "always on"));
        }

        /// <summary>Resume everything Focused suspended, right now.</summary>
        private void ThawNow()
        {
            _headless.Fire("POST", "/api/v1/thaw", "{}");
            Logger.LogInfo("asked Focused to resume every suspended target");
        }

        private void SetDryRun(bool dryRun)
        {
            _dryRun.Value = dryRun;
            ApplySettingsNow();
            Logger.LogInfo("dry-run set to " + dryRun);
        }

        private void ScanNow()
        {
            _headless.Fire("POST", "/api/v1/scan", "{}");
        }

        /// <summary>
        /// Push a settings change (made in the panel) into the runner at once.
        /// </summary>
        /// <remarks>
        /// The runner usually picks changes up from the config fingerprint on its
        /// own tick; when the user flips a switch, waiting up to a tick to see the
        /// effect is exactly the kind of lag that makes a UI feel broken.
        /// </remarks>
        private void ApplySettingsNow()
        {
            var runner = _runner;
            if (runner == null)
            {
                return;
            }

            _lastConfigFingerprint = null;
            runner.ApplySettings(ReadSettings());
        }

        /// <summary>Short message for the overlay, from the settings window.</summary>
        private void RunnerToast(string message, bool warning)
        {
            var runner = _runner;
            if (runner != null)
            {
                runner.ShowToast(message, warning);
            }
            else
            {
                Logger.LogInfo("[panel] " + message);
            }
        }

        private void PersistHudMode(HudMode value)
        {
            _hudMode.Value = value;
            _lastConfigFingerprint = null;
        }

        /// <summary>
        /// A cheap value snapshot of everything the runner cares about.
        /// Deliberately just string concatenation: it runs four times a second,
        /// and correctness matters far more than micro-optimisation here.
        /// </summary>
        private string ConfigFingerprint()
        {
            return string.Join(
                "|",
                _runInBackground.Value.ToString(),
                _hudMode.Value.ToString(),
                _hudCorner.Value,
                _toastSeconds.Value.ToString("R"),
                _pollInterval.Value.ToString("R"),
                _focusedUrl.Value,
                _focusedToken.Value,
                _requestTimeoutMs.Value.ToString(),
                _processNames.Value,
                _cmdlineSubstrings.Value,
                _protectedNames.Value,
                _protectedCmdlineSubstrings.Value);
        }

        private void SyncSettings()
        {
            if (_runner == null)
            {
                return;
            }

            var settings = ReadSettings();
            var current = _runner.CurrentSettings;
            if (current == null || !settings.EndpointEquals(current))
            {
                _runner.SetClient(BuildClient(settings));
            }

            _runner.ApplySettings(settings);
        }

        private FocusSettings ReadSettings()
        {
            return new FocusSettings
            {
                Hud = _hudMode.Value,
                HudCorner = _hudCorner.Value,
                ToastSeconds = _toastSeconds.Value,
                PollIntervalSeconds = _pollInterval.Value,
                FocusedUrl = _focusedUrl.Value,
                FocusedToken = _focusedToken.Value,
                RequestTimeoutMs = _requestTimeoutMs.Value,
                Enabled = _pluginEnabled.Value,
                AlwaysOn = _alwaysOn.Value,
                DryRun = _dryRun.Value,
                TimerFreshSeconds = _timerFreshSeconds.Value,
                SessionLeaseSeconds = _sessionLeaseSeconds.Value,
                ProcessNames = RuleText.ToArray(_processNames.Value),
                CmdlineSubstrings = RuleText.ToArray(_cmdlineSubstrings.Value),
                ProtectedNames = RuleText.ToArray(_protectedNames.Value),
                ProtectedCmdlineSubstrings = RuleText.ToArray(_protectedCmdlineSubstrings.Value)
            };
        }

        private FocusedClient BuildClient(FocusSettings settings)
        {
            return new FocusedClient(
                settings.FocusedUrl, settings.FocusedToken, settings.RequestTimeoutMs);
        }

        private void LogSettingsProblems(FocusSettings settings)
        {
            var problems = settings.Validate();
            for (var i = 0; i < problems.Length; i++)
            {
                Logger.LogWarning(problems[i]);
            }
        }
    }
}
