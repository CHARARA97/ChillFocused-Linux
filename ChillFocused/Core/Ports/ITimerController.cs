namespace ChillFocused.Core
{
    internal enum TimerControlStatus
    {
        Applied,
        Unsupported,
        Failed
    }

    internal sealed class TimerControlResult
    {
        public TimerControlStatus Status;
        public string Message;

        internal static TimerControlResult Applied()
        {
            return new TimerControlResult { Status = TimerControlStatus.Applied };
        }

        internal static TimerControlResult Unsupported(string message)
        {
            return new TimerControlResult { Status = TimerControlStatus.Unsupported, Message = message };
        }

        internal static TimerControlResult Failed(string message)
        {
            return new TimerControlResult { Status = TimerControlStatus.Failed, Message = message };
        }
    }

    /// <summary>Writes the game's timer. Kept separate from the read-only ITimerSource.</summary>
    internal interface ITimerController
    {
        bool CanControl { get; }
        TimerControlResult Apply(SessionPlan plan);
        TimerControlResult SyncTime(TimerMode mode, float seconds);
        TimerControlResult Reset();
    }
}

namespace ChillFocused.Core
{
    /// <summary>Controller used when the game adapter is not wired (tests/tools).</summary>
    internal sealed class NullTimerController : ITimerController
    {
        public bool CanControl { get { return false; } }
        public TimerControlResult Apply(SessionPlan plan) { return TimerControlResult.Unsupported("no game timer"); }
        public TimerControlResult SyncTime(TimerMode mode, float seconds) { return TimerControlResult.Unsupported("no game timer"); }
        public TimerControlResult Reset() { return TimerControlResult.Unsupported("no game timer"); }
    }
}
