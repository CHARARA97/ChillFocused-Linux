using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Follows the game's own language setting so the panels speak it too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game registers a <c>LanguageSupplier</c> in its DI container; asking it
    /// beats guessing from the system locale, because the player may well have set
    /// the game to a language their desktop does not use.  If the setting cannot be
    /// read, the panel keeps whatever language it was already using -- the plugin
    /// has always shipped Chinese first, so that stays the default.
    /// </para>
    /// <para>
    /// Unity-free (it uses <see cref="Environment.TickCount"/>) so the mapping is
    /// unit tested and the polling can be exercised without a game.
    /// </para>
    /// </remarks>
    internal static class GameLanguageProbe
    {
        public const string SupplierTypeName = "Bulbul.LanguageSupplier";

        //: The setting changes at most a few times a session (from a menu); five
        //: seconds keeps it responsive without a per-frame reflection call.
        private const int PollMilliseconds = 5000;

        //: How long the setting may stay unreadable before it is worth a warning.
        //: Until the first scene loads there is no supplier at all, and saying
        //: "something is wrong" during that window would be a false alarm.
        private const int SlowMilliseconds = 30000;

        private static int _nextPollAt;
        private static int _firstAttemptAt;
        private static bool _reportedOnce;
        private static bool _reportedMissing;
        private static bool _reportedSlow;

        /// <summary>The language the game reported, or null while unknown.</summary>
        public static string DetectedName { get; private set; }

        /// <summary>Whether the game's language supplier has been reached.</summary>
        public static bool SupplierFound { get; private set; }

        /// <summary>
        /// Maps the game's language enum name onto ours.  Names rather than values:
        /// the numeric order is the game's business and can change between builds.
        /// </summary>
        public static GameLanguage Map(string gameLanguageName)
        {
            if (string.IsNullOrEmpty(gameLanguageName))
            {
                return GameLanguage.English;
            }

            var name = gameLanguageName.Trim();
            if (name.IndexOf("Japanese", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GameLanguage.Japanese;
            }

            if (name.IndexOf("Traditional", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GameLanguage.ChineseTraditional;
            }

            if (name.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Simplified", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GameLanguage.ChineseSimplified;
            }

            // English is the game's own fallback, so it is ours too.
            return GameLanguage.English;
        }

        /// <summary>
        /// Polls the game's setting.  Cheap to call from a tick: it returns
        /// immediately unless the interval has elapsed.
        /// </summary>
        /// <param name="resolve">
        /// How to reach the supplier.  Injectable so the behaviour here -- what gets
        /// logged, and when -- can be tested without a game.
        /// </param>
        public static void Tick(Action<string> log = null, Action<string> warn = null,
                                bool force = false, Func<string, bool, object> resolve = null,
                                int now = 0)
        {
            if (now == 0)
            {
                now = Environment.TickCount;
            }

            if (!force && now < _nextPollAt)
            {
                return;
            }

            _nextPollAt = now + PollMilliseconds;
            if (_firstAttemptAt == 0)
            {
                _firstAttemptAt = now;
            }

            var lookUp = resolve ?? GameServices.Resolve;
            var supplier = lookUp(SupplierTypeName, force);
            var name = supplier == null ? null : ReadLanguageName(supplier);
            if (name == null)
            {
                // Say so once.  Otherwise the panels keep their default and nobody --
                // user or maintainer -- can tell that apart from "the game is set to
                // that language".
                if (log != null && !_reportedMissing)
                {
                    _reportedMissing = true;
                    log("game language: not readable yet; panel text stays " +
                        PanelText.Language + " (DI container found: " +
                        GameServices.ContainerFound + ")");
                }

                // Still nothing after half a minute: that is worth an eyebrow.
                if (warn != null && !_reportedSlow && now - _firstAttemptAt > SlowMilliseconds)
                {
                    _reportedSlow = true;
                    warn("game language: still unreadable after " + (SlowMilliseconds / 1000) +
                         "s; the panels keep their current language. Please report this line " +
                         "together with the DI container state below.");
                }

                return;
            }

            SupplierFound = true;
            DetectedName = name;
            var language = Map(name);
            PanelText.Language = language;

            // Reported once on the first successful read, even when it agrees with the
            // default: "the detection works and the game is in Chinese" and "the
            // detection never ran" must not look the same in a log.
            if (log != null && !_reportedOnce)
            {
                _reportedOnce = true;
                log("game language: " + name + " -> panel text " + language);
            }
        }

        /// <summary>The name of the enum value the supplier reports, or null.</summary>
        private static string ReadLanguageName(object supplier)
        {
            try
            {
                var type = supplier.GetType();
                var get = type.GetMethod("Get", Type.EmptyTypes);
                if (get == null)
                {
                    return null;
                }

                var value = get.Invoke(supplier, null);
                if (value == null)
                {
                    return null;
                }

                // A nullable enum comes back boxed as the enum itself, but be safe.
                var valueType = value.GetType();
                if (valueType.IsEnum)
                {
                    return value.ToString();
                }

                var valueProperty = valueType.GetProperty("Value");
                if (valueProperty != null && valueType.IsGenericType)
                {
                    var inner = valueProperty.GetValue(value, null);
                    return inner == null ? null : inner.ToString();
                }

                return value.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Forgets the polling state (used by tests).</summary>
        internal static void Reset()
        {
            _nextPollAt = 0;
            _firstAttemptAt = 0;
            _reportedOnce = false;
            _reportedMissing = false;
            _reportedSlow = false;
            DetectedName = null;
            SupplierFound = false;
            PanelText.Language = GameLanguage.ChineseSimplified;
        }
    }
}
