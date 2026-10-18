using System;

namespace ChillFocused.Core
{
    /// <summary>The timer state source the runner and sync loop consume.</summary>
    /// <remarks>
    /// The real implementation is a Unity/Harmony probe. Keeping this port in the
    /// pure layer lets tests inject a fake timer and keeps FocusRunner from calling
    /// static platform code directly.
    /// </remarks>
    internal interface ITimerSource
    {
        TimerState Snapshot();
        event Action<TimerState> Changed;
        void Install(Action<string> log);
    }
}
