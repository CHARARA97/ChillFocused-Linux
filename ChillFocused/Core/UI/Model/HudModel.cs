namespace ChillFocused.Core
{
    /// <summary>
    /// Everything the overlay needs for one frame, and nothing about where it came
    /// from. The runner builds this from network/timer state; the renderer only
    /// reads it.
    /// </summary>
    /// <remarks>
    /// This is the seam that makes UI debugging cheap: tests can construct a model
    /// and assert the exact lines without a game, HTTP server or Unity instance.
    /// </remarks>
    public sealed class HudModel
    {
        public HudMode Mode = HudMode.Minimal;
        public TextLookup Text;
        public bool Connected;
        public bool Enforcing;
        public bool GateSatisfied;
        public bool DryRun;
        public bool Degraded;
        public string Version = "?";
        public int FrozenCount;
        public int FrozenTotal;
        public int RuleNameCount;
        public string[] FrozenNames = new string[0];
        public string[] RecentNames = new string[0];
        public bool TimerKnown;
        public bool TimerWorking;
        public string ToggleHudKey = string.Empty;
        public string ToggleSettingsKey = string.Empty;
        public string LastWarning = string.Empty;
    }
}
