using System;
using BepInEx.Configuration;

namespace ChillFocused.Core
{
    /// <summary>Dependencies supplied by the plugin when the overlay is created.</summary>
    internal sealed class FocusRunnerWiring
    {
        public FocusRunnerServices Services;
        public FocusRunnerPanel Panel;
        public FocusRunnerHotkeys Hotkeys;
        public FocusRunnerActions Actions;
    }

    internal sealed class FocusRunnerServices
    {
        public FocusSettings Settings;
        public IFocusedEndpoint Client;
        public Func<FocusSettings> SettingsProvider;
        public ITimerSource TimerSource;
        public Action RequestImmediateSync;
        public Action DumpDebug;
        public Action<ExternalSessionState> OnExternalSession;
        public Action<TimerState> OnGameTimer;
    }

    internal sealed class FocusRunnerPanel
    {
        public ConfigBridge Config;
        public string SpecPath;
        public string TextPath;
    }

    internal sealed class FocusRunnerHotkeys
    {
        public ConfigEntry<KeyboardShortcut> ToggleHud;
        public ConfigEntry<KeyboardShortcut> ToggleSettings;
        public ConfigEntry<KeyboardShortcut> ReloadUi;
        public Action<HudMode> PersistHud;
    }

    internal sealed class FocusRunnerActions
    {
        public Action<bool> SetGameGate;
        public Action<bool> SetDryRun;
        public Action ThawNow;
        public Action ScanNow;
    }
}
