namespace ChillFocused.Core
{
    internal enum ExternalSessionMode
    {
        Unknown,
        Countdown,
        Pomodoro,
        Countup
    }

    /// <summary>A pure snapshot of Focused's manual session, without wire types.</summary>
    internal sealed class ExternalSessionState
    {
        public bool Active;
        public bool Manual;
        public ExternalSessionMode Mode;
        public int WorkMinutes;
        public int BreakMinutes;
        public int Cycles;
        public float RemainingSeconds;
        public float ElapsedSeconds;
        public float Since;

        internal static ExternalSessionState Inactive()
        {
            return new ExternalSessionState { Mode = ExternalSessionMode.Unknown };
        }
    }
}
