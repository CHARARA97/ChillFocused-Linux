using System;

namespace ChillFocused.Core
{
    /// <summary>What the plugin wants the game's timer to become.</summary>
    internal sealed class SessionPlan : IEquatable<SessionPlan>
    {
        public TimerMode Mode;
        public int WorkMinutes;
        public int BreakMinutes;
        public int Cycles;

        internal static SessionPlan Pomodoro(int workMinutes, int breakMinutes, int cycles)
        {
            return new SessionPlan
            {
                Mode = TimerMode.Pomodoro,
                WorkMinutes = workMinutes < 1 ? 1 : workMinutes,
                BreakMinutes = breakMinutes < 0 ? 0 : breakMinutes,
                Cycles = cycles < 1 ? 1 : cycles,
            };
        }

        internal static SessionPlan Countup()
        {
            return new SessionPlan { Mode = TimerMode.Countup, Cycles = 1 };
        }

        public bool Equals(SessionPlan other)
        {
            return other != null
                   && Mode == other.Mode
                   && WorkMinutes == other.WorkMinutes
                   && BreakMinutes == other.BreakMinutes
                   && Cycles == other.Cycles;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SessionPlan);
        }

        public override int GetHashCode()
        {
            return ((int)Mode * 397) ^ WorkMinutes ^ (BreakMinutes << 8) ^ (Cycles << 16);
        }
    }

    /// <summary>Maps a Focused session to the game's two timer modes.</summary>
    internal static class SessionPlanFactory
    {
        internal static SessionPlan From(ExternalSessionState state)
        {
            if (state == null || !state.Active || !state.Manual)
            {
                return null;
            }

            switch (state.Mode)
            {
                case ExternalSessionMode.Countdown:
                    // A countdown is one Pomodoro work cycle. Whole minutes only.
                    if (state.RemainingSeconds <= 0f)
                    {
                        return null;
                    }

                    var minutes = (int)Math.Ceiling(state.RemainingSeconds / 60.0);
                    return SessionPlan.Pomodoro(minutes, 0, 1);

                case ExternalSessionMode.Pomodoro:
                    if (state.WorkMinutes < 1 || state.BreakMinutes < 0 || state.Cycles < 1)
                    {
                        return null;
                    }

                    return SessionPlan.Pomodoro(
                        state.WorkMinutes,
                        state.BreakMinutes,
                        state.Cycles);

                case ExternalSessionMode.Countup:
                    return SessionPlan.Countup();

                default:
                    return null;
            }
        }
    }
}
