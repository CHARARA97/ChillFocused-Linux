using System;
using System.IO;
using BepInEx;
using ChillFocused.Core;
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
        // A config *identifier*: renaming the section would orphan every setting a
        // user already has saved under it.  The wording rule covers what people read.
        private const string SectionBlacklist = "2. Blacklist";
        private const string SectionProtect = "3. Protect";
        private const string SectionOverlay = "4. Overlay";
        private const string SectionHotkeys = "5. Hotkeys";

        /// <summary>How often the configuration file is re-checked for edits.</summary>
        private const float ConfigCheckSeconds = 0.25f;

        private PluginConfig _options;
        private ConfigBridge _bridge;

        private RuntimeHost _runtime;
        private PluginActions _actions;
        private float _nextConfigCheckAt;
        private bool _loggedFirstTick;
        private float _nextHeartbeatAt;
        private int _heartbeatsLeft = 12;
        private FrameDriver _frameDriver;
        private GameHookInstaller _gameHooks;
        private string _configPath;
        private PluginInstanceLock _instanceLock;
        private ConfigWatcher _configWatcher;
        private ITimerSource _timerSource;
        private ISessionReportGate _reportGate;

        /// <summary>Panel layout file, hot-reloaded by the runner while the game runs.</summary>
        private PanelFiles _panelFiles;
        private MainThreadPump _pump;

        private void Awake()
        {
            _options = new PluginConfig(Config);

            // A second copy of the DLL must not create a second overlay or sync
            // thread. Acquire this before installing hooks or starting anything.
            try
            {
                _configPath = Config != null ? Config.ConfigFilePath : null;
            }
            catch (Exception)
            {
                _configPath = null;
            }

            var lockPath = string.IsNullOrEmpty(_configPath)
                ? Path.Combine(Path.GetTempPath(), "com.chillfocused.instance.lock")
                : Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "com.chillfocused.instance.lock");
            string lockError;
            _instanceLock = PluginInstanceLock.TryAcquire(lockPath, out lockError);
            if (_instanceLock == null)
            {
                LogError(
                    "another ChillFocused instance already owns " + lockPath +
                    " (" + lockError + "); this copy will stay inert. Remove the duplicate DLL from BepInEx/plugins.");
                enabled = false;
                return;
            }

            // Log the focus state before anything else can influence it: if the
            // frame loop never ticks, this line is the only evidence about why.
            LogInfo(
                "focus state at Awake: isFocused=" + Application.isFocused +
                ", runInBackground=" + Application.runInBackground);

            if (_options.RunInBackground.Value && !Application.runInBackground)
            {
                Application.runInBackground = true;
                LogInfo("set Application.runInBackground = true to keep the frame loop alive");
            }

            _frameDriver = new FrameDriver(TickCore);
            _gameHooks = new GameHookInstaller(
                _frameDriver,
                message => LogInfo(message),
                message => LogWarning(message));
            _gameHooks.Install();
            _pump = new MainThreadPump(
                message => LogInfo(message),
                message => LogWarning(message));
            _pump.Install();

            // Recreate the overlay object once a scene has actually finished
            // loading. Objects created while the chainloader runs are created
            // before the first scene exists, so DontDestroyOnLoad does not take
            // effect and they are destroyed almost immediately.
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                LogInfo("subscribed to SceneManager.sceneLoaded");
            }
            catch (Exception ex)
            {
                LogWarning("could not subscribe to SceneManager.sceneLoaded: " + ex.Message);
            }

            // Kept as an extra source in case some future render path does drive
            // it; it costs nothing and the tick scheduler dedupes by frame.
            try
            {
                Application.onBeforeRender += OnBeforeRenderTick;
            }
            catch (Exception ex)
            {
                LogWarning("could not subscribe to Application.onBeforeRender: " + ex.Message);
            }

            _bridge = new ConfigBridge(
                _options.HudMode, _options.ProcessNames, _options.CmdlineSubstrings,
                _options.ProtectedNames, _options.MirrorExternalSession,
                _options.MirrorTimeSyncSeconds, _options.LockTimerControls);

            // Read the game's timer state from the game's own services. This is what
            // makes a session start or stop take effect immediately instead of after
            // Focused's save-file window.
            _timerSource = new GameTimerSource();
            _timerSource.Install(message => LogInfo(message));
            _reportGate = new SessionReportGate();

            // Written next to the plugin's own config so it is easy to find, and
            // created on first run with every key and range documented: a layout
            // file nobody knows about is not a debugging aid.
            _panelFiles = PanelFiles.Ensure(
                _configPath,
                message => LogInfo(message),
                message => LogWarning(message));

            var settings = ReadSettings();
            LogSettingsProblems(settings);
            _configWatcher = new ConfigWatcher(
                _configPath,
                () => Config.Reload(),
                () => _options.Fingerprint(),
                SyncSettings,
                message => LogInfo(message));
            _configWatcher.MarkCurrent();

            _runtime = new RuntimeHost(new RuntimeHostContext
            {
                SettingsProvider = ReadSettingsFresh,
                RunnerFactory = CreateRunner,
                ClientFactory = BuildClient,
                TimerSource = _timerSource,
                ReportGate = _reportGate,
                TimerControllerFactory = () => new GameTimerController(LogWarning),
                OnCycle = () => _pump.Post(EnsureRunner),
                LogInfo = message => LogInfo(message),
                LogWarning = message => LogWarning(message),
                LogError = message => LogError(message),
            });

            _actions = new PluginActions(
                _options, _runtime, _configWatcher, ReadSettings,
                message => LogInfo(message));

            _runtime.EnsureRunner(settings);

            // The sync thread is the part that must work unconditionally, so it
            // is started last and independently of anything Unity-related above.
            _runtime.StartSync(settings);

            LogInfo(
                FocusPluginInfo.Name + " " + FocusPluginInfo.FullVersion + " loaded from " +
                (Info != null && !string.IsNullOrEmpty(Info.Location) ? Info.Location : "?") +
                "; backend at " + _options.FocusedUrl.Value + ".");
            LogInfo(
                "the session decision follows the game's own timer and is handed to Focused as a " +
                "lease; the suspending itself happens outside the game.");
            LogInfo("panel language: starting as " + PanelText.Language +
                           "; it follows the game's setting once the probe reads it " +
                           "(override any string in com.chillfocused.text.cfg)");
            PluginEnvironmentReport.ReportForeignCopies(
                Paths.PluginPath,
                Info != null ? Info.Location : null,
                message => LogWarning(message),
                message => LogDebug(message));
        }

        /// <summary>Recreate the overlay after a scene transition destroys it.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogInfo("scene loaded: '" + scene.name + "' (" + mode + ")");
            EnsureRunner();
        }

        private void OnBeforeRenderTick()
        {
            _frameDriver.Tick("Application.onBeforeRender", Time.frameCount);
        }

        private void Update()
        {
            _frameDriver.Tick("Update", Time.frameCount);
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
        private void TickCore(string source)
        {
            if (!_loggedFirstTick)
            {
                _loggedFirstTick = true;
                LogInfo("first tick arrived via " + source + " (frame " + Time.frameCount + ")");
            }

            // A short heartbeat makes "is Unity still ticking, and does it stop
            // when the window loses focus?" answerable from the log alone.
            if (_heartbeatsLeft > 0 && Time.unscaledTime >= _nextHeartbeatAt)
            {
                _heartbeatsLeft--;
                _nextHeartbeatAt = Time.unscaledTime + 5f;
                LogInfo(
                    "heartbeat via " + source + ": frame=" + Time.frameCount +
                    " focused=" + Application.isFocused +
                    " runnerAlive=" + (_runtime != null && _runtime.Runner != null) +
                    " mainPosts=" + (_pump != null ? _pump.Posts : 0) +
                    " mainRuns=" + (_pump != null ? _pump.Runs : 0));
            }

            EnsureRunner();
            if (_runtime != null && _runtime.Runner != null)
            {
                _runtime.Runner.Tick();
            }

            if (Time.unscaledTime < _nextConfigCheckAt)
            {
                return;
            }

            _nextConfigCheckAt = Time.unscaledTime + ConfigCheckSeconds;
            if (_configWatcher != null)
            {
                _configWatcher.Tick();
            }
        }

        /// <summary>
        /// Create the runner object and wire it up.
        /// </summary>
        /// <summary>Create the Unity overlay object and hand it to the host.</summary>
        private IFocusRunner CreateRunner(FocusSettings settings)
        {
            var host = new GameObject("ChillFocusedRunner");
            host.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<FocusRunner>();
            runner.LogInfo = message => LogInfo(message);
            runner.LogWarning = message => LogWarning(message);
            runner.Initialise(BuildWiring(settings));
            return runner;
        }

        private FocusRunnerWiring BuildWiring(FocusSettings settings)
        {
            return new FocusRunnerWiring
            {
                Services = new FocusRunnerServices
                {
                    Settings = settings,
                    Client = BuildClient(settings),
                    SettingsProvider = ReadSettingsFresh,
                    TimerSource = _timerSource,
                    RequestImmediateSync = () => _runtime.Wake(),
                    DumpDebug = _runtime.DumpDebug,
                    OnExternalSession = _runtime.ObserveExternalSession,
                    OnGameTimer = _runtime.ObserveGameTimer,
                },
                Panel = new FocusRunnerPanel
                {
                    Config = _bridge,
                    SpecPath = _panelFiles.SpecPath,
                    TextPath = _panelFiles.TextPath,
                },
                Hotkeys = new FocusRunnerHotkeys
                {
                    ToggleHud = _options.ToggleHud,
                    ToggleSettings = _options.ToggleSettings,
                    ReloadUi = _options.ReloadUi,
                    PersistHud = _actions.PersistHudMode,
                },
                Actions = new FocusRunnerActions
                {
                    SetGameGate = _actions.SetGameGate,
                    SetDryRun = _actions.SetDryRun,
                    ThawNow = _actions.ThawNow,
                    ScanNow = _actions.ScanNow,
                },
            };
        }

        private void EnsureRunner()
        {
            if (_runtime != null)
            {
                _runtime.EnsureRunner(ReadSettings());
            }
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
        private FocusSettings ReadSettingsFresh()
        {
            if (_configWatcher != null)
            {
                _configWatcher.ReloadIfChanged();
            }

            return ReadSettings();
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
            LogInfo("OnApplicationFocus(" + focused + ")");
        }

        private void OnApplicationPause(bool paused)
        {
            LogInfo("OnApplicationPause(" + paused + ")");
        }

        private void OnDestroy()
        {
            // Keep the instance lock for the process lifetime. This game destroys
            // the plugin object moments after Awake; releasing the lock there would
            // let a duplicate DLL become active later. The OS releases the handle
            // when the process exits.

            // Diagnostic: if this appears while the game is still running, the
            // game destroyed the plugin's own GameObject, which is why no Unity
            // message ever reaches this plugin afterwards.
            LogInfo("plugin OnDestroy: the game is destroying the plugin object" +
                           (HasTicked ? " (after " + (_frameDriver != null ? _frameDriver.LastFrame : -1) + " frames)" : " (never ticked)"));

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
                if (_gameHooks != null)
                {
                    _gameHooks.Uninstall();
                }
            }
            catch (Exception)
            {
                // shutting down; nothing useful to do
            }

            if (_runtime != null)
            {
                _runtime.DestroyRunner();
            }
        }

        // -- configuration ----------------------------------------------------

        private void SyncSettings()
        {
            if (_runtime != null)
            {
                _runtime.SyncSettings();
            }
        }

        private FocusSettings ReadSettings()
        {
            return _options.ReadSettings();
        }

        private IFocusedEndpoint BuildClient(FocusSettings settings)
        {
            return new FocusedClient(
                settings.FocusedUrl, settings.FocusedToken, settings.RequestTimeoutMs,
                new JsonUtilityCodec(), message => LogError(message));
        }

        private void LogInfo(string message)
        {
            Logger.LogInfo(LogEvent.Prefixed(message));
        }

        private void LogWarning(string message)
        {
            Logger.LogWarning(LogEvent.Prefixed(message));
        }

        private void LogError(string message)
        {
            Logger.LogError(LogEvent.Prefixed(message));
        }

        private void LogDebug(string message)
        {
            Logger.LogDebug(LogEvent.Prefixed(message));
        }

        private void LogSettingsProblems(FocusSettings settings)
        {
            var problems = settings.Validate();
            for (var i = 0; i < problems.Length; i++)
            {
                LogWarning(problems[i]);
            }
        }
    }
}
