using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

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

        private static object _core;
        private static object _pomodoro;
        private static object _pomodoroTimer;
        private static object _countup;
        private static object _countupTimer;
        private static object _timerParent;

        // Last values read, for diagnostics.

        /// <summary>Raised on the main thread whenever the state actually changes.</summary>
        internal static event Action<TimerState> Changed;

        internal static object PomodoroService
        {
            get { lock (Sync) { return _pomodoro; } }
        }

        internal static object CountupService
        {
            get { lock (Sync) { return _countup; } }
        }

        internal static object PomodoroTimerService
        {
            get { lock (Sync) { return _pomodoroTimer; } }
        }

        internal static object CountupTimerService
        {
            get { lock (Sync) { return _countupTimer; } }
        }

        internal static object TimerParentUI
        {
            get
            {
                lock (Sync)
                {
                    if (_timerParent != null)
                    {
                        return _timerParent;
                    }
                }

                // FacilityPomodoro.Setup injects the same parent UI; this is the
                // fallback for a scene where the facility has not run yet.
                try
                {
                    var type = AccessTools.TypeByName("Bulbul.TimerParentUI");
                    if (type == null || !typeof(Component).IsAssignableFrom(type))
                    {
                        return null;
                    }

                    var found = UnityEngine.Object.FindObjectOfType(type);
                    if (found == null)
                    {
                        var all = Resources.FindObjectsOfTypeAll(type);
                        if (all.Length > 0)
                        {
                            found = all[0];
                        }
                    }

                    if (found != null)
                    {
                        lock (Sync)
                        {
                            _timerParent = found;
                        }
                    }

                    return found;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

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
                    "Bulbul.CountupService",
                    new[] { "StartCountup", "PlayCountupTimer", "PlayOrPauseCountupTimer", "Pause", "UnPause", "ResetTimer" }),
                Tuple.Create(
                    "Bulbul.CountupTimerService",
                    new[] { "PlayTimer", "PauseTimer", "UnpauseTimer", "ResetTimer" }),
                Tuple.Create(
                    "Bulbul.PomodoroService",
                    new[] { "Setup", "StartPomodoro", "PlayPomodoroTimer", "PlayOrPausePomodoroTimer", "Pause", "UnPause", "OnTimerEnd" }),
                Tuple.Create(
                    "Bulbul.FacilityPomodoro",
                    new[] { "Setup" }),
                Tuple.Create(
                    "Bulbul.FacilityCountup",
                    new[] { "Setup" }),
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

            var methodPatches = patched;
            var constructorPatches = PatchConstructors("Bulbul.PomodoroService")
                                     + PatchConstructors("Bulbul.CountupService");

            log("timer probe: hooked " + methodPatches + " of " + candidates
                + " timer method(s), " + constructorPatches + " constructor(s)");
        }

        private static int PatchConstructors(string typeName)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return 0;
            }

            var patched = 0;
            foreach (var ctor in type.GetConstructors(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    var harmony = new Harmony("com.chillfocused.timer.ctor." + typeName);
                    harmony.Patch(
                        ctor,
                        null,
                        new HarmonyMethod(typeof(GameTimerProbe).GetMethod(
                            "CaptureInstance",
                            BindingFlags.Static | BindingFlags.NonPublic)));
                    patched++;
                }
                catch (Exception)
                {
                    // Constructor shapes differ between game builds; the method
                    // hooks and the scene/DI fallbacks remain available.
                }
            }

            return patched;
        }

        private static void CaptureInstance(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            Remember(__instance, __instance.GetType().Name);
        }

        private static void RememberField(object owner, string fieldName, string serviceName)
        {
            if (owner == null)
            {
                return;
            }

            try
            {
                var field = owner.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var value = field == null ? null : field.GetValue(owner);
                if (value != null)
                {
                    Remember(value, serviceName);
                }
            }
            catch (Exception)
            {
                // Field is a compatibility hook, not a hard dependency.
            }
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
                RememberField(instance, "_pomodoroTimerService", "PomodoroTimerService");
            }
            else if (serviceName == "PomodoroTimerService")
            {
                _pomodoroTimer = instance;
            }
            else if (serviceName == "CountupService")
            {
                _countup = instance;
                RememberField(instance, "_countupTimerService", "CountupTimerService");
            }
            else if (serviceName == "CountupTimerService")
            {
                _countupTimer = instance;
            }
            else if (serviceName == "FacilityPomodoro")
            {
                RememberField(instance, "_pomodoroService", "PomodoroService");
                RememberField(instance, "_timerParentUI", "TimerParentUI");
            }
            else if (serviceName == "FacilityCountup")
            {
                RememberField(instance, "_countupService", "CountupService");
            }
            else if (serviceName == "TimerParentUI")
            {
                _timerParent = instance;
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

            if (_pomodoroTimer == null)
            {
                Remember(GameServices.Resolve("Bulbul.PomodoroTimerService"), "PomodoroTimerService");
            }

            if (_countup == null)
            {
                Remember(GameServices.Resolve("Bulbul.CountupService"), "CountupService");
            }

            if (_countupTimer == null)
            {
                Remember(GameServices.Resolve("Bulbul.CountupTimerService"), "CountupTimerService");
            }

            // The VContainer resolve path is not always available before the game
            // has created its timer UI. These scene components hold the same
            // service instances, so use them as a second source.
            if (_pomodoro == null)
            {
                var service = FromComponentField("Bulbul.FacilityPomodoro", "_pomodoroService");
                if (service != null) Remember(service, "FacilityPomodoro");
            }

            if (_countup == null)
            {
                var service = FromComponentField("Bulbul.FacilityCountup", "_countupService");
                if (service != null) Remember(service, "FacilityCountup");
            }
        }

        //: Poll-style hooks (Update) fire every frame; the start/stop hooks are rare.
        private const float PollIntervalSeconds = 0.2f;
        private static float _lastPollAt;

        private static object ReadServiceField(Type type, UnityEngine.Object[] components, string fieldName)
        {
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                return null;
            }

            for (var i = 0; i < components.Length; i++)
            {
                var value = field.GetValue(components[i]);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static object FromComponentField(string typeName, string fieldName)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null || !typeof(Component).IsAssignableFrom(type))
                {
                    return null;
                }

                var components = UnityEngine.Object.FindObjectsOfType(type);
                var value = ReadServiceField(type, components, fieldName);
                if (value != null)
                {
                    return value;
                }

                // Inactive UI objects are not included by FindObjectsOfType.
                components = Resources.FindObjectsOfTypeAll(type);
                return ReadServiceField(type, components, fieldName);
            }
            catch (Exception)
            {
                // A scene lookup is best-effort; the VContainer path may still work.
            }

            return null;
        }

        private static void Refresh(string hit)
        {
            if (_core == null || _pomodoro == null || _countup == null)
            {
                ResolveMissingServices();
            }

            string raw;
            var next = TimerStateReader.Read(
                _core, _pomodoro, _pomodoroTimer, _countup, _countupTimer,
                hit, GameClock.Now, out raw);

            // Announce each distinct source once, so the log shows which hooks fire
            // without flooding it.
            if (_announced.Add(hit) && _log != null)
            {
                _log("timer probe: first " + raw);
            }

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

    }
}
