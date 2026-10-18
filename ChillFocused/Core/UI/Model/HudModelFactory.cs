using System;

namespace ChillFocused.Core
{
    /// <summary>Builds the pure HUD projection from the status snapshot.</summary>
    internal static class HudModelFactory
    {
        internal static HudModel Build(FocusSettings settings, FocusStatus status,
                                       string toggleHudKey, string toggleSettingsKey,
                                       TextLookup translate)
        {
            return new HudModel
            {
                Mode = settings.Hud,
                Text = translate,
                Connected = status.Connected,
                Enforcing = status.Connected && status.SessionActive,
                GateSatisfied = status.GateSatisfied,
                DryRun = status.DryRun,
                Degraded = status.Degraded,
                Version = status.Version,
                FrozenCount = status.FrozenCount,
                FrozenTotal = status.FrozenTotal,
                RuleNameCount = settings.ProcessNames.Length,
                FrozenNames = status.FrozenNames,
                RecentNames = status.RecentNames,
                TimerKnown = status.Timer.Known,
                TimerWorking = status.Timer.Working,
                ToggleHudKey = toggleHudKey,
                ToggleSettingsKey = toggleSettingsKey,
                LastWarning = status.LastWarning,
            };
        }
    }
}
