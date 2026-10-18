using System;

namespace ChillFocused.Core
{
    /// <summary>Timer source used when no game probe is installed (tests/tools).</summary>
    internal sealed class NullTimerSource : ITimerSource
    {
        public TimerState Snapshot()
        {
            return default(TimerState);
        }

        public event Action<TimerState> Changed
        {
            add { }
            remove { }
        }

        public void Install(Action<string> log)
        {
        }
    }
}
