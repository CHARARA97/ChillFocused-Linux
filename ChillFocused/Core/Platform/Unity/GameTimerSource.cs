using System;

namespace ChillFocused.Core
{
    /// <summary>Adapter from the static Harmony probe to <see cref="ITimerSource"/>.</summary>
    internal sealed class GameTimerSource : ITimerSource
    {
        public TimerState Snapshot()
        {
            return GameTimerProbe.Snapshot();
        }

        public event Action<TimerState> Changed
        {
            add { GameTimerProbe.Changed += value; }
            remove { GameTimerProbe.Changed -= value; }
        }

        public void Install(Action<string> log)
        {
            GameTimerProbe.Install(log);
        }
    }
}
