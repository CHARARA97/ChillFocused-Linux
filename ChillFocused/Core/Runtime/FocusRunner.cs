using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Unity-side focus loop. It owns polling and the injected native page; there is
    /// no IMGUI/HUD renderer in this path anymore.
    /// </summary>
    internal sealed class FocusRunner : MonoBehaviour, IFocusRunner
    {
        private const float BasePollFloor = 0.2f;
        private const int MaxPumpActionsPerFrame = 16;

        private FocusSettings _settings = new FocusSettings();
        private IFocusedEndpoint _client;
        private ConfigEntry<KeyboardShortcut> _toggleSettings;
        private ConfigEntry<KeyboardShortcut> _reloadUi;
        private HotkeyController _hotkeys;
        private Func<FocusSettings> _settingsProvider;
        private Action _requestImmediateSync;
        private Action<ExternalSessionState> _onExternalSession;
        private Action<TimerState> _onGameTimer;
        private Action _dumpDebug;
        private SettingsRefresher _settingsRefresher;
        private ITimerSource _timerSource;
        private StatusCoordinator _state;
        private SettingsPageInjector _settingsInjector;
        private ConfigBridge _config;
        private readonly TimerControlGuard _timerControls = new TimerControlGuard();
        private bool _loggedFirstUpdate;
        private int _tickedFrame = -1;

        public Action<string> LogInfo;
        public Action<string> LogWarning;

        private void Inform(string message)
        {
            if (LogInfo != null) LogInfo(message);
            else Debug.Log("[ChillFocused] " + message);
        }

        private void Warn(string message)
        {
            if (LogWarning != null) LogWarning(message);
            else Debug.LogWarning("[ChillFocused] " + message);
        }

        public bool IsAlive { get { return this != null; } }
        public FocusSettings CurrentSettings { get { return _settings; } }

        public void Initialise(FocusRunnerWiring wiring)
        {
            if (wiring == null)
            {
                Inform("Initialise called without wiring");
                return;
            }

            var services = wiring.Services;
            _toggleSettings = wiring.Hotkeys.ToggleSettings;
            _reloadUi = wiring.Hotkeys.ReloadUi;
            _hotkeys = new HotkeyController(null, _toggleSettings, _reloadUi);
            _settingsProvider = services.SettingsProvider;
            _settingsRefresher = new SettingsRefresher(_settingsProvider);
            _timerSource = services.TimerSource;
            _requestImmediateSync = services.RequestImmediateSync;
            _onExternalSession = services.OnExternalSession;
            _onGameTimer = services.OnGameTimer;
            _dumpDebug = services.DumpDebug;
            _client = services.Client;

            _state = new StatusCoordinator(new StatusPresenter(AddToast, Inform), Warn,
                delegate
                {
                    if (_requestImmediateSync != null) _requestImmediateSync();
                }, _onExternalSession);
            _state.SetClient(_client);
            _state.OnTimer(_timerSource.Snapshot());

            var actions = wiring.Actions;
            _config = wiring.Panel.Config;
            _settingsInjector = new SettingsPageInjector(wiring.Panel.Config, _client,
                () => _state.Status,
                actions.SetGameGate, AddToast, Inform);

            _timerSource.Changed += OnTimerStateChanged;
            ApplySettings(services.Settings);
            AddToast("ChillFocused 已就绪 - F7 打开原生设置页", false);
        }

        public void ShowToast(string message, bool warning)
        {
            AddToast(message, warning);
        }

        public void StopExternalSession()
        {
            if (_client != null) _client.StopSessionAsync(null);
        }

        private void AddToast(string text, bool warning)
        {
            if (warning) Warn(text);
            else Inform(text);
        }

        private void Start()
        {
            Inform("runner Start() reached; the component is live in scene '" +
                   UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "'");
        }

        private void Update()
        {
            Tick();
        }

        public void Tick()
        {
            if (_client == null || _tickedFrame == Time.frameCount) return;
            _tickedFrame = Time.frameCount;

            if (!_loggedFirstUpdate)
            {
                _loggedFirstUpdate = true;
                Inform("runner is ticking; executor " + _client.BaseUrl +
                       ", poll every " + _settings.PollIntervalSeconds.ToString("0.##") + "s");
            }

            GameLanguageProbe.Tick(Inform, Warn);
            _client.Pump(MaxPumpActionsPerFrame);
            _hotkeys.Tick(null, TogglePanel, ReloadUi);
            if (_settingsInjector != null)
            {
                _settingsInjector.Tick();
                if (Input.GetKeyDown(KeyCode.F8)) _settingsInjector.OpenChillFocus();
            }

            if (_config != null)
            {
                _timerControls.Tick(_config.LockTimerControls, _state.Status.Timer, Inform);
            }

            if (_settingsRefresher.TryRefresh(Time.unscaledTime, ref _settings))
            {
                // The page reads the settings snapshot on every build; nothing to invalidate.
            }

            Poll();
        }

        private void OnGUI()
        {
            // IMGUI is only used as a key-event fallback on Wine; it draws nothing.
            _hotkeys.OnGui(Event.current, null, TogglePanel, ReloadUi);
        }

        private void OnDestroy()
        {
            _timerControls.ReleaseAll();
            if (_timerSource != null) _timerSource.Changed -= OnTimerStateChanged;
            Inform("runner destroyed");
        }

        private void Poll()
        {
            var interval = Mathf.Max(BasePollFloor, _settings.PollIntervalSeconds);
            _state.Poll(Time.unscaledTime, interval, _settings,
                () => AddToast("已连接到 Focused", false));
        }

        public void ReloadUi()
        {
            if (_settingsProvider != null)
            {
                var fresh = _settingsProvider();
                if (fresh != null) _settings = fresh;
            }

            Inform(LogEvent.Line("ui.reload"));
            if (_dumpDebug != null) _dumpDebug();
        }

        private void OnTimerStateChanged(TimerState state)
        {
            _state.OnTimer(state);
            if (_onGameTimer != null) _onGameTimer(state);
        }

        private void TogglePanel()
        {
            if (_settingsInjector != null) _settingsInjector.ToggleSettings();
        }

        public void ApplySettings(FocusSettings settings)
        {
            if (settings != null) _settings = settings;
        }

        public void SetClient(IFocusedEndpoint client)
        {
            _client = client;
            _state.SetClient(client);
        }

        public PluginDebugSnapshot CaptureDebug()
        {
            return new PluginDebugSnapshot
            {
                Connected = _state.Status.Connected,
                Version = _state.Status.Version,
                Degraded = _state.Status.Degraded,
                SessionActive = _state.Status.SessionActive,
                GateSatisfied = _state.Status.GateSatisfied,
                DryRun = _state.Status.DryRun,
                TimerKnown = _state.Status.Timer.Known,
                TimerMode = _state.Status.Timer.Mode,
                TimerWorking = _state.Status.Timer.Working,
                TimerRunning = _state.Status.Timer.Running,
                TimerResting = _state.Status.Timer.Resting,
                TimerPhase = _state.Status.Timer.Phase,
                TimerSource = _state.Status.Timer.Source,
                FrozenCount = _state.Status.FrozenCount,
                FrozenTotal = _state.Status.FrozenTotal,
                LastWarning = _state.Status.LastWarning,
                PollEventCursor = _state.Poller.EventCursor,
                PollNextAt = _state.Poller.NextPollAt,
                PollCount = _state.Poller.RequestCount,
                LastPollMs = _state.Poller.LastRequestMs,
                MaxPollMs = _state.Poller.MaxRequestMs,
                UpdateFrames = 0,
                GuiFrames = 0,
                LastDrawMs = 0,
                MaxDrawMs = 0,
            };
        }

        public void DestroyRunner()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
