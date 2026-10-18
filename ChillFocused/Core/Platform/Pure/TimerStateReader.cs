using System;
using System.Reflection;

namespace ChillFocused.Core
{
    /// <summary>Parses the game's timer services into <see cref="TimerState"/>.</summary>
    /// <remarks>
    /// Service names and method names still come from the game, but the decision
    /// table (Pomodoro vs Countup, running vs working, phase) lives here so it can
    /// be tested with plain fake service objects. Harmony installation remains in
    /// GameTimerProbe.
    /// </remarks>
    internal static class TimerStateReader
    {
        internal static TimerState Read(object core, object pomodoro, object pomodoroTimer,
                                        object countup, object countupTimer,
                                        string hit, float now, out string raw)
        {
            bool? coreWorking = CallBool(core, "IsCurrentWorking");
            bool? coreResting = CallBool(core, "IsCurrentResting");
            bool? coreRunning = CallBool(core, "IsTimerRunning");
            bool? pomodoroTimerRunning = CallBoolField(pomodoroTimer, "_isRunning");
            bool? countupTimerRunning = CallBoolField(countupTimer, "_isRunning");
            bool? pomodoroRunning = CallBool(pomodoro, "IsTimerRunning");
            bool? pomodoroWorking = CallBool(pomodoro, "IsCurrentWorking");
            bool? pomodoroResting = CallBool(pomodoro, "IsCurrentResting");

            // Count-up is a separate service. Its public contract is Play/Pause;
            // the state query names have differed, so probe likely names.
            bool? countupRunning = CallBool(countup, "IsTimerRunning")
                                   ?? CallBool(countup, "IsRunning")
                                   ?? CallBool(countup, "IsPlaying");
            bool? countupWorking = CallBool(countup, "IsCurrentWorking");

            var phase = CallName(pomodoro, "CurrentPomodoroType");

            raw = string.Format(
                "{0} | core run={1} work={2} rest={3} | pomodoro run={4} work={5} rest={6} phase={7} | timer run={8} | countup run={9} work={10} | countup timer run={11}",
                hit, Show(coreRunning), Show(coreWorking), Show(coreResting),
                Show(pomodoroRunning), Show(pomodoroWorking), Show(pomodoroResting), phase ?? "?",
                Show(pomodoroTimerRunning), Show(countupRunning), Show(countupWorking),
                Show(countupTimerRunning));

            bool? working = coreWorking ?? pomodoroWorking;
            if (coreWorking.HasValue && pomodoroWorking.HasValue && coreWorking.Value != pomodoroWorking.Value)
            {
                working = pomodoroWorking;
            }

            var mode = TimerMode.Unknown;
            bool? running = null;

            if (countupRunning == true || countupWorking == true)
            {
                mode = TimerMode.Countup;
                // Countup has no break phase: IsCurrentWorking is the run/pause
                // signal. IsTimerRunning can remain true while paused, just like
                // PomodoroService, so it cannot be used alone.
                working = countupWorking ?? true;
                running = countupRunning ?? working;
                if (working == false)
                {
                    running = false;
                }
                phase = null;
            }
            else if (pomodoroRunning == true || (phase != null && phase.Length > 0))
            {
                mode = TimerMode.Pomodoro;
            }
            else if (countupRunning.HasValue || countupWorking.HasValue)
            {
                mode = TimerMode.Countup;
                working = false;
                running = false;
                phase = null;
            }
            else if (pomodoroRunning.HasValue || pomodoroWorking.HasValue)
            {
                mode = TimerMode.Pomodoro;
            }

            // The low-level timer owns the actual play/pause bit. Choose the
            // timer for the active mode; the other timer can exist and be idle.
            bool? activeTimerRunning = null;
            if (mode == TimerMode.Countup)
            {
                activeTimerRunning = countupTimerRunning
                                     ?? (countupWorking == false ? false : countupRunning);
            }
            else if (mode == TimerMode.Pomodoro)
            {
                activeTimerRunning = pomodoroTimerRunning ?? pomodoroRunning;
            }

            running = activeTimerRunning ?? coreRunning ?? working;

            return new TimerState
            {
                Known = mode != TimerMode.Unknown && (working.HasValue || running.HasValue),
                Mode = mode,
                Working = working ?? false,
                Running = running ?? false,
                Source = hit,
                ChangedAt = now,
                ReadAt = now,
                Phase = phase,
                Resting = mode == TimerMode.Pomodoro
                          && (coreResting == true || pomodoroResting == true),
            };
        }

        private static string Show(bool? value)
        {
            return value.HasValue ? (value.Value ? "true" : "false") : "n/a";
        }

        private static string CallName(object instance, string method)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                var property = type.GetProperty(method);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    var value = property.GetValue(instance, null);
                    return value == null ? null : value.ToString();
                }

                var info = type.GetMethod(method, Type.EmptyTypes)
                           ?? type.GetMethod("get_" + method, Type.EmptyTypes);
                if (info == null)
                {
                    return null;
                }

                var result = info.Invoke(instance, null);
                return result == null ? null : result.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool? CallBoolField(object instance, string field)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var info = instance.GetType().GetField(
                    field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (info == null)
                {
                    return null;
                }

                var value = info.GetValue(instance);
                return value is bool ? (bool?)value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool? CallBool(object instance, string method)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                var info = type.GetMethod(
                    method,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (info == null)
                {
                    return null;
                }

                var value = info.Invoke(instance, null);
                return value is bool ? (bool?)value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
