using System;

namespace ChillFocused.Core
{
    /// <summary>Wire response → pure external-session snapshot.</summary>
    internal static class ExternalSessionMapper
    {
        internal static ExternalSessionState From(FocusStateResponse response)
        {
            if (response == null)
            {
                return ExternalSessionState.Inactive();
            }

            return new ExternalSessionState
            {
                Active = response.active,
                Manual = response.manual_session,
                Mode = ParseMode(response.session_mode),
                WorkMinutes = ToMinutes(response.session_work_minutes),
                BreakMinutes = ToMinutes(response.session_break_minutes),
                Cycles = response.session_cycles < 1 ? 0 : response.session_cycles,
                RemainingSeconds = response.session_remaining,
                ElapsedSeconds = response.session_elapsed,
                Since = response.since,
            };
        }

        private static ExternalSessionMode ParseMode(string mode)
        {
            if (string.Equals(mode, "countdown", StringComparison.OrdinalIgnoreCase))
            {
                return ExternalSessionMode.Countdown;
            }

            if (string.Equals(mode, "pomodoro", StringComparison.OrdinalIgnoreCase))
            {
                return ExternalSessionMode.Pomodoro;
            }

            if (string.Equals(mode, "stopwatch", StringComparison.OrdinalIgnoreCase))
            {
                return ExternalSessionMode.Countup;
            }

            return ExternalSessionMode.Unknown;
        }

        private static int ToMinutes(float value)
        {
            return value <= 0f ? 0 : (int)Math.Round(value);
        }
    }
}
