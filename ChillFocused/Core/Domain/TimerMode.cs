namespace ChillFocused.Core
{
    /// <summary>Which of the game's timer kinds produced a reading.</summary>
    /// <remarks>
    /// The booleans ("working"/"running") are ambiguous across timer kinds: a
    /// break and a stopped count-up both look like "not working".  Keeping the
    /// mode explicit lets the session policy decide each kind on its own terms.
    /// </remarks>
    public enum TimerMode
    {
        /// <summary>No reliable mode; the policy must fail closed.</summary>
        Unknown = 0,
        Pomodoro,
        Countup
    }
}
