using System;
using System.Reflection;

namespace ChillFocused.Core
{
    /// <summary>
    /// Gets the game's service objects out of its own dependency-injection container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game registers its timers and its language setting in a VContainer
    /// <c>ProjectLifetimeScope</c> (namespace <c>NestopiSystem.DIContainers</c>), which
    /// other mods for this game already resolve from.  Asking the container is
    /// cheaper and far less brittle than hooking the methods that happen to hold a
    /// reference: a Harmony patch depends on a method still existing and still
    /// receiving the call, while <c>Resolve&lt;T&gt;()</c> only depends on the type
    /// still being registered -- and both are guarded, because a game update can
    /// change either.
    /// </para>
    /// <para>
    /// Everything is by name and reflection on purpose: this plugin does not
    /// reference the game's assembly, so a build works against any version of it.
    /// Nothing here throws; a failure returns <c>null</c> and the caller falls back.
    /// </para>
    /// </remarks>
    internal static class GameServices
    {
        public const string ContainerTypeName = "NestopiSystem.DIContainers.ProjectLifetimeScope";

        //: Resolving is cheap but not free, and callers poll.  One attempt a second
        //: is plenty: the container appears once a scene has loaded.
        private const int RetryMilliseconds = 1000;

        private static readonly object Sync = new object();
        private static bool _resolvedContainer;
        private static Type _containerType;
        private static MethodInfo _resolveMethod;
        private static int _nextAttemptAt;

        /// <summary>Whether the container has been found (for diagnostics).</summary>
        public static bool ContainerFound
        {
            get { lock (Sync) { return _containerType != null; } }
        }

        /// <summary>
        /// The registered instance of <paramref name="typeName"/>, or <c>null</c> when
        /// the container is not up yet, the type is not registered, or anything at all
        /// goes wrong.
        /// </summary>
        public static object Resolve(string typeName, bool force = false)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            try
            {
                lock (Sync)
                {
                    var now = Environment.TickCount;
                    if (!force && _containerType == null && now < _nextAttemptAt)
                    {
                        return null;
                    }

                    if (_resolveMethod == null && !Prepare())
                    {
                        _nextAttemptAt = Environment.TickCount + RetryMilliseconds;
                        return null;
                    }

                    var serviceType = FindType(typeName);
                    if (serviceType == null)
                    {
                        _nextAttemptAt = Environment.TickCount + RetryMilliseconds;
                        return null;
                    }

                    var generic = _resolveMethod.MakeGenericMethod(serviceType);
                    return generic.Invoke(null, null);
                }
            }
            catch (Exception)
            {
                // A container that throws is a container we do not use this time.
                lock (Sync)
                {
                    _nextAttemptAt = Environment.TickCount + RetryMilliseconds;
                }

                return null;
            }
        }

        private static bool Prepare()
        {
            _containerType = FindType(ContainerTypeName);
            if (_containerType == null)
            {
                return false;
            }

            var methods = _containerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (var i = 0; i < methods.Length; i++)
            {
                var candidate = methods[i];
                if (candidate.Name == "Resolve"
                    && candidate.IsGenericMethodDefinition
                    && candidate.GetParameters().Length == 0)
                {
                    _resolveMethod = candidate;
                    _resolvedContainer = true;
                    return true;
                }
            }

            return false;
        }

        private static Type FindType(string name)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length; i++)
                {
                    Type found;
                    try
                    {
                        found = assemblies[i].GetType(name, false);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch (Exception)
            {
                // GetAssemblies can throw in exotic runtimes; treat it as "not found".
            }

            return null;
        }

        /// <summary>Resets the cached lookups (tests, and a scene change if needed).</summary>
        internal static void Reset()
        {
            lock (Sync)
            {
                _containerType = null;
                _resolveMethod = null;
                _resolvedContainer = false;
                _nextAttemptAt = 0;
            }
        }

        /// <summary>True once the container has been located at least once.</summary>
        internal static bool ContainerWasFound
        {
            get { lock (Sync) { return _resolvedContainer; } }
        }
    }
}
