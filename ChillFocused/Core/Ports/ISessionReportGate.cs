namespace ChillFocused.Core
{
    /// <summary>
    /// Decides whether the game-timer heartbeat may claim a session.
    /// </summary>
    /// <remarks>
    /// While an external Focused session owns the game timer, the plugin must not
    /// send its own game-timer claim. Entering external ownership also requests one
    /// withdrawal so a previous claim cannot keep Focused active after the handoff.
    /// </remarks>
    internal interface ISessionReportGate
    {
        bool CanReport { get; }
        void EnterExternal();
        void LeaveExternal();
        bool TryConsumeWithdrawal();
    }
}
