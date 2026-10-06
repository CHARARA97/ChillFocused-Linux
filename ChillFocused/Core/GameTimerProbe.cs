using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ChillFocused.Core
{
    /// <summary>
    /// Reads the game's timer state from the game's own services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polling the save file costs up to 25 seconds, because the game only rewrites
    /// its timer stamp every 6-15 seconds. The game itself knows immediately, and a
    /// dump of its API shows exactly where:
    /// </para>
    /// <list type="bullet">
    /// <item><c>Bulbul.PomodoroService</c> -- <c>IsTimerRunning</c>,
    /// <c>IsCurrentWorking</c>, <c>IsCurrentResting</c>, plus
    /// <c>StartPomodoro</c> / <c>PlayPomodoroTimer</c> / <c>Pause</c> /
    /// <c>UnPause</c> / <c>OnTimerEnd</c>.</item>
    /// <item><c>Bulbul.CountupTimerService</c> -- <c>PlayTimer</c> /
    /// <c>PauseTimer</c> / <c>UnpauseTimer</c>, for the count-up mode.</item>
    /// <item><c>Bulbul.TimerCoreService</c> -- <c>IsCurrentWorking</c> /
    /// <c>IsCurrentResting</c> and the phase callbacks
    /// <c>OnWorkTimer</c> / <c>OnBreakTimer</c> / <c>OnPauseTimer</c> /
    /// <c>OnIdleTimer</c>.</item>
    /// </list>
    /// <para>
    /// Hooking those gives the transition on the frame it happens. Both mechanisms
    /// are used: the phase callbacks and the explicit start/stop methods report a
    /// change, and the captured service instances are then queried for the
    /// authoritative state. Anything the query cannot express is remembered from
    /// the hooks.
    /// </para>
    /// <para>
    /// This is deliberately independent of Focused's save-file gate. That gate
    /// stays as the fallback for when the plugin is not loaded, so Focused never
    /// depends on the game or the plugin being alive.
    /// </para>
    /// </remarks>
    internal static class GameTimerProbe
    {
        private static readonly object Sync = new object();

        private static bool _installed;
        private static Action<string> _log;
        private static TimerState _state;
        private static readonly HashSet<string> _announced = new HashSet<string>();
        private static readonly HashSet<string> _reflectionFailures = new HashSet<string>();

        private static object _core;
        private static object _pomodoro;
        private static object _countup;

        // Last values read, for diagnostics.
        private static string _lastRaw = string.Empty;

        /// <summary>Raised on the main thread whenever the state actually changes.</summary>
        internal static event Action<TimerState> Changed;

        internal static TimerState Snapshot()
        {
            lock (Sync)
            {
                return _state;
            }
        }

        internal static void Install(Action<string> log)
        {
            lock (Sync)
            {
                if (_installed)
                {
                    return;
                }

                _installed = true;
            }

            _log = log;

            var plan = new[]
            {
                Tuple.Create(
                    "Bulbul.TimerCoreService",
                    new[] { "Update", "StartTimer", "OnWorkTimer", "OnBreakTimer", "OnPauseTimer", "OnIdleTimer" }),
                Tuple.Create(
                    "Bulbul.PomodoroTimerService",
                    new[] { "PlayTimer", "PauseTimer", "UnpauseTimer", "ResetTimer" }),
                Tuple.Create(
                    "Bulbul.CountupTimerService",
                    new[] { "PlayTimer", "PauseTimer", "UnpauseTimer", "ResetTimer" }),
                Tuple.Create(
                    "Bulbul.PomodoroService",
                    new[] { "StartPomodoro", "PlayPomodoroTimer", "PlayOrPausePomodoroTimer", "Pause", "UnPause", "OnTimerEnd" }),
            };

            var patched = 0;
            var candidates = 0;

            foreach (var entry in plan)
            {
                var type = AccessTools.TypeByName(entry.Item1);
                if (type == null)
                {
                    log("timer probe: type not found: " + entry.Item1);
                    continue;
                }

                foreach (var name in entry.Item2)
                {
                    foreach (var method in type.GetMethods(
                                 BindingFlags.Instance | BindingFlags.Public |
                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != name || method.IsAbstract)
                        {
                            continue;
                        }

                        candidates++;
                        try
                        {
                            var harmony = new Harmony("com.chillfocused.timer." + entry.Item1 + "." + name);
                            harmony.Patch(
                                method,
                                null,
                                new HarmonyMethod(typeof(GameTimerProbe).GetMethod(
                                    "Postfix",
                                    BindingFlags.Static | BindingFlags.NonPublic)));
                            patched++;
                        }
                        catch (Exception ex)
                        {
                            log("timer probe: cannot patch " + entry.Item1 + "." + name + ": " + ex.Message);
                        }
                    }
                }
            }

            log("timer probe: hooked " + patched + " of " + candidates + " timer method(s)");
        }

        /// <summary>Harmony postfix: remember the instance, then re-read the state.</summary>
        private static void Postfix(object __instance, MethodBase __originalMethod)
        {
            var label = __originalMethod == null
                ? "?"
                : __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;

            Remember(__instance, __originalMethod);

            // Reading four properties per frame would be wasteful; five times a
            // second is still indistinguishable from instant. The explicit
            // start/stop hooks always report at once.
            var pollStyle = label.EndsWith(".Update", StringComparison.Ordinal);
            if (pollStyle)
            {
                var now = UnityEngine.Time.unscaledTime;
                if (now - _lastPollAt < PollIntervalSeconds)
                {
                    return;
                }

                _lastPollAt = now;
            }

            Refresh(label);
        }

        private static void Remember(object instance, MethodBase method)
        {
            if (instance == null || method == null)
            {
                return;
            }

            Remember(instance, method.DeclaringType.Name);
        }

        private static void Remember(object instance, string serviceName)
        {
            if (instance == null || string.IsNullOrEmpty(serviceName))
            {
                return;
            }

            if (serviceName == "TimerCoreService")
            {
                _core = instance;
            }
            else if (serviceName == "PomodoroService")
            {
                _pomodoro = instance;
            }
            else if (serviceName == "CountupTimerService")
            {
                _countup = instance;
            }
        }

        /// <summary>
        /// Fill any service the hooks have not seen yet by asking the game's own DI
        /// container.
        /// </summary>
        /// <remarks>
        /// Two independent ways of getting the same object on purpose.  Hooking a
        /// method depends on that method still existing and still being called; asking
        /// <c>ProjectLifetimeScope.Resolve&lt;T&gt;()</c> depends on the type still
        /// being registered.  A game update can break either one, and the plugin only
        /// needs one of them to work.  Never throws: a miss simply leaves the field
        /// alone for the hooks.
        /// </remarks>
        private static void ResolveMissingServices()
        {
            if (_core == null)
            {
                Remember(GameServices.Resolve("Bulbul.TimerCoreService"), "TimerCoreService");
            }

            if (_pomodoro == null)
            {
                Remember(GameServices.Resolve("Bulbul.PomodoroService"), "PomodoroService");
            }

            if (_countup == null)
            {
                Remember(GameServices.Resolve("Bulbul.CountupTimerService"), "CountupTimerService");
            }
        }

        //: Poll-style hooks (Update) fire every frame; the start/stop hooks are rare.
        private const float PollIntervalSeconds = 0.2f;
        private static float _lastPollAt;

        private static void Refresh(string hit)
        {
            if (_core == null || _pomodoro == null || _countup == null)
            {
                ResolveMissingServices();
            }

            bool? coreWorking = CallBool(_core, "IsCurrentWorking");
            bool? coreResting = CallBool(_core, "IsCurrentResting");
            bool? pomodoroRunning = CallBool(_pomodoro, "IsTimerRunning");
            bool? pomodoroWorking = CallBool(_pomodoro, "IsCurrentWorking");
            bool? pomodoroResting = CallBool(_pomodoro, "IsCurrentResting");

            // The phase, straight from the game: Work / Break / Complete.
            var phase = CallName(_pomodoro, "CurrentPomodoroType");

            var raw = string.Format(
                "{0} | core work={1} rest={2} | pomodoro run={3} work={4} rest={5} phase={6}",
                hit, Show(coreWorking), Show(coreResting), Show(pomodoroRunning),
                Show(pomodoroWorking), Show(pomodoroResting), phase ?? "?");

            // Announce each distinct source once, so the log shows which hooks fire
            // without flooding it.
            if (_announced.Add(hit) && _log != null)
            {
                _log("timer probe: first " + raw);
            }

            bool? working = coreWorking ?? pomodoroWorking;
            if (coreWorking.HasValue && pomodoroWorking.HasValue && coreWorking.Value != pomodoroWorking.Value)
            {
                // Disagreement between the two services: trust the Pomodoro service,
                // which is the one that models the work phase.
                working = pomodoroWorking;
            }

            var running = pomodoroRunning ?? working;

            var next = new TimerState
            {
                Known = working.HasValue || running.HasValue,
                Working = working ?? false,
                Running = running ?? false,
                Source = hit,
                ChangedAt = UnityEngine.Time.unscaledTime,
                ReadAt = UnityEngine.Time.unscaledTime,
                Phase = phase,
                Resting = coreResting == true || pomodoroResting == true,
            };

            Publish(next, raw);
        }

        private static void Publish(TimerState next, string raw)
        {
            TimerState previous;
            lock (Sync)
            {
                previous = _state;
                if (previous.SameAs(next))
                {
                    // Same values, new reading: liveness is about the reading, so the
                    // timestamp has to move even when nothing else does.
                    _state.ReadAt = next.ReadAt;
                    _lastRaw = raw;
                    return;
                }

                _state = next;
            }

            if (_log != null)
            {
                _log(string.Format(
                    "timer probe: {0} (working={1} running={2}) via {3}",
                    next.Known ? "state changed" : "state unknown",
                    next.Working,
                    next.Running,
                    raw));
            }

            var handler = Changed;
            if (handler != null)
            {
                try
                {
                    handler(next);
                }
                catch (Exception)
                {
                    // A reporting failure must never break the game's timer code.
                }
            }
        }

        private static string Show(bool? value)
        {
            return value.HasValue ? (value.Value ? "true" : "false") : "n/a";
        }

        /// <summary>The name of an enum-returning, no-argument method, or null.</summary>
        private static string CallName(object instance, string method)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var type = instance.GetType();

                // CurrentPomodoroType is a *property*: the metadata calls it
                // get_CurrentPomodoroType, and looking only for a method of the plain
                // name silently returned null (the log showed phase=? for a whole run).
                var property = type.GetProperty(method);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    var fromProperty = property.GetValue(instance, null);
                    return fromProperty == null ? null : fromProperty.ToString();
                }

                var info = type.GetMethod(method, System.Type.EmptyTypes)
                           ?? type.GetMethod("get_" + method, System.Type.EmptyTypes);
                if (info == null)
                {
                    return null;
                }

                var value = info.Invoke(instance, null);
                return value == null ? null : value.ToString();
            }
            catch (System.Exception)
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
                    NoteOnce(method, "timer probe: " + type.Name + " has no " + method + "()");
                    return null;
                }

                var value = info.Invoke(instance, null);
                if (value is bool)
                {
                    return (bool)value;
                }

                NoteOnce(method, "timer probe: " + type.Name + "." + method + "() is not a bool");
                return null;
            }
            catch (Exception ex)
            {
                NoteOnce(method, "timer probe: " + method + "() threw " + ex.GetType().Name);
                return null;
            }
        }

        private static void NoteOnce(string key, string message)
        {
            lock (Sync)
            {
                if (!_reflectionFailures.Add(key))
                {
                    return;
                }
            }

            if (_log != null)
            {
                _log(message);
            }
        }
    }
}
