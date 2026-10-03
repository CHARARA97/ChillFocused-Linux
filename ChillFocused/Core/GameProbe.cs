using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Hooks into the game's own code by reflection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things are needed that the plugin cannot get any other way:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Observation of the game's mode.</b> The user wants interception only
    /// while the game is in its creative/work mode, so the mod has to know which
    /// mode the game is in. Reading that needs a look inside the game's own
    /// types.
    /// </description></item>
    /// <item><description>
    /// <b>A moment after the game has settled.</b> Everything created while
    /// BepInEx runs its chainloader is destroyed before it ever receives a frame
    /// callback. A Harmony postfix runs on the main thread from inside the game's
    /// own call stack, which is late enough to create an object that might
    /// actually survive.
    /// </description></item>
    /// </list>
    /// <para>
    /// Types are resolved by name with <see cref="AccessTools"/> rather than by
    /// referencing <c>Assembly-CSharp.dll</c> at compile time. That keeps the
    /// plugin buildable without the game present, and means a missing or renamed
    /// game type degrades to a log line instead of a load failure.
    /// </para>
    /// <para>
    /// The candidate targets are the ones a published mod for this game
    /// (RealTimeWeatherMod) patches successfully, so they are known to exist:
    /// <c>Bulbul.CurrentDateAndTimeUI.UpdateDateAndTime</c> runs whenever the
    /// in-game clock updates, and <c>Bulbul.UnlockConditionService.IsUnlocked</c>
    /// runs frequently.
    /// </para>
    /// </remarks>
    internal static class GameProbe
    {
        /// <summary>Invoked on the main thread from inside game code.</summary>
        internal static Action Tick;

        /// <summary>Type name that exposes the current high-level game mode.</summary>
        private const string StateTypeName = "RoomGameManager";

        private const string StatePropertyName = "CurrentMainState";

        private static readonly (string Type, string Method)[] Candidates =
        {
            ("Bulbul.CurrentDateAndTimeUI", "UpdateDateAndTime"),
            ("Bulbul.UnlockConditionService", "IsUnlocked"),
        };

        private static readonly List<string> Patched = new List<string>();

        internal static IReadOnlyList<string> PatchedTargets
        {
            get { return Patched; }
        }

        /// <summary>
        /// Patch every candidate that resolves. Returns how many were patched.
        /// </summary>
        internal static int Install(Action<string> log, Action<string> warn)
        {
            var harmony = new Harmony("com.chillfocused.gameprobe");
            var postfix = new HarmonyMethod(AccessTools.Method(typeof(GameProbe), "Postfix"));

            foreach (var candidate in Candidates)
            {
                try
                {
                    var type = AccessTools.TypeByName(candidate.Type);
                    if (type == null)
                    {
                        warn("game type not found: " + candidate.Type);
                        continue;
                    }

                    var method = AccessTools.Method(type, candidate.Method);
                    if (method == null)
                    {
                        warn("game method not found: " + candidate.Type + "." + candidate.Method);
                        continue;
                    }

                    harmony.Patch(method, postfix: postfix);
                    Patched.Add(candidate.Type + "." + candidate.Method);
                    log("patched " + candidate.Type + "." + candidate.Method);
                }
                catch (Exception ex)
                {
                    warn("could not patch " + candidate.Type + "." + candidate.Method +
                         ": " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            return Patched.Count;
        }

        /// <summary>
        /// Reads the game's current high-level mode, or null if it cannot be read.
        /// </summary>
        /// <remarks>
        /// <c>Resources.FindObjectsOfTypeAll</c> walks every loaded object, so
        /// this is called at most about once a second.
        /// </remarks>
        internal static string ReadGameState()
        {
            try
            {
                var type = AccessTools.TypeByName(StateTypeName);
                if (type == null)
                {
                    return null;
                }

                var found = Resources.FindObjectsOfTypeAll(type);
                if (found == null || found.Length == 0)
                {
                    return null;
                }

                var property = type.GetProperty(
                    StatePropertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null)
                {
                    return "no-property";
                }

                var value = property.GetValue(found[0], null);
                return value == null ? "null" : value.ToString();
            }
            catch (Exception ex)
            {
                return "error:" + ex.GetType().Name;
            }
        }

        /// <summary>
        /// Log the game's own types and methods, filtered by name.
        /// </summary>
        /// <remarks>
        /// Choosing a Harmony target by guessing method names wasted several
        /// rounds: the patches installed cleanly and then never fired. This dumps
        /// the real API surface so the next choice is made from facts.
        /// Output is capped so a single run cannot flood the log.
        /// </remarks>
        internal static void DumpApi(Action<string> log, string[] filters, int maxTypes, int maxMembers)
        {
            var dumped = 0;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "Assembly-CSharp")
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = Array.FindAll(ex.Types, type => type != null);
                    }

                    log("Assembly-CSharp type count: " + types.Length);

                    foreach (var type in types)
                    {
                        if (dumped >= maxTypes)
                        {
                            log("... dump truncated at " + maxTypes + " types");
                            return;
                        }

                        if (!MatchesFilter(type.Name, filters))
                        {
                            continue;
                        }

                        dumped++;
                        log("TYPE " + type.FullName);

                        var shown = 0;
                        foreach (var method in type.GetMethods(
                                     BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.DeclaredOnly))
                        {
                            if (method.IsSpecialName || shown >= maxMembers)
                            {
                                continue;
                            }

                            shown++;
                            var parameters = method.GetParameters();
                            var names = new string[parameters.Length];
                            for (var i = 0; i < parameters.Length; i++)
                            {
                                names[i] = parameters[i].ParameterType.Name;
                            }

                            log("    " + (method.IsStatic ? "static " : string.Empty) +
                                method.ReturnType.Name + " " + method.Name +
                                "(" + string.Join(", ", names) + ")");
                        }

                        foreach (var property in type.GetProperties(
                                     BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.DeclaredOnly))
                        {
                            log("    prop " + property.PropertyType.Name + " " + property.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log("api dump failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool MatchesFilter(string typeName, string[] filters)
        {
            if (filters == null || filters.Length == 0)
            {
                return true;
            }

            foreach (var filter in filters)
            {
                if (typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Describes what the probe managed to attach to, for the log.</summary>
        internal static string Describe()
        {
            if (Patched.Count == 0)
            {
                return "(none)";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < Patched.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Patched[i]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The postfix itself. Wrapped so a fault here can never propagate into
        /// the game's own call stack.
        /// </summary>
        private static void Postfix()
        {
            var tick = Tick;
            if (tick == null)
            {
                return;
            }

            try
            {
                tick();
            }
            catch (Exception)
            {
                // Never let the mod break the game.
            }
        }
    }
}
