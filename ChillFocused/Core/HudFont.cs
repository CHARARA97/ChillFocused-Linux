using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Finds a font that can actually draw Chinese.
    /// </summary>
    /// <remarks>
    /// Unity's built-in IMGUI font carries no CJK glyphs, so Chinese text renders
    /// as empty boxes. The game is localised into Chinese and therefore already
    /// has a CJK font loaded, so the first and best option is to borrow it. Failing
    /// that, an OS font is requested by name -- under Proton that goes through Wine's
    /// font handling, which is backed by the host's fontconfig.
    /// </remarks>
    internal static class HudFont
    {
        private static Font _resolved;
        private static bool _searched;
        private static string _source = "(not searched)";
        private static string _preferredName = string.Empty;
        private static Font _preferredFont;

        internal static string Source
        {
            get { return _source; }
        }

        //: A character that only a CJK-capable font can supply.
        private const char Probe = '\u4e2d';

        /// <summary>Resolve, trying a configured family name first.</summary>
        internal static Font Resolve(string preferred)
        {
            if (!string.IsNullOrEmpty(preferred))
            {
                if (_preferredName == preferred && _preferredFont != null)
                {
                    return _preferredFont;
                }

                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(preferred, 14);
                    if (font != null && font.dynamic)
                    {
                        _preferredName = preferred;
                        _preferredFont = font;
                        _source = "the configured font \"" + preferred + "\"";
                        return font;
                    }
                }
                catch (Exception)
                {
                    // fall through to auto-detection
                }
            }

            return Resolve();
        }

        internal static Font Resolve()
        {
            if (_searched)
            {
                return _resolved;
            }

            _searched = true;

            // Ordering matters. The game always has a dynamic font loaded, but it
            // is usually Unity's built-in LegacyRuntime, which has no CJK glyphs at
            // all: Chinese then only appears through the OS font fallback, with
            // metrics that disagree with the Latin text. So a font that really
            // contains CJK is preferred, and the fallback is the last resort.
            _resolved = FromTextMeshPro();
            if (_resolved != null)
            {
                _source = "the game's TextMeshPro font";
                return _resolved;
            }

            _resolved = FromOsFonts();
            if (_resolved != null)
            {
                _source = "an OS font with CJK coverage";
                return _resolved;
            }

            _resolved = FromLoadedDynamicFonts();
            if (_resolved != null)
            {
                _source = "the game's loaded font " + _resolved.name +
                          " (Latin only; CJK comes from font fallback)";
                return _resolved;
            }

            _source = "none; Chinese will render as boxes";
            return null;
        }

        /// <summary>The game's own CJK font, reached through its TMP font asset.</summary>
        private static Font FromTextMeshPro()
        {
            try
            {
                var type = AccessTools.TypeByName("TMPro.TMP_FontAsset");
                if (type == null)
                {
                    return null;
                }

                var property = type.GetProperty(
                    "sourceFontFile",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null)
                {
                    return null;
                }

                foreach (var candidate in Resources.FindObjectsOfTypeAll(type))
                {
                    var font = property.GetValue(candidate, null) as Font;
                    if (font != null && font.dynamic && font.HasCharacter(Probe))
                    {
                        return font;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the other options.
            }

            return null;
        }

        private static Font FromLoadedDynamicFonts()
        {
            try
            {
                Font any = null;
                foreach (var candidate in Resources.FindObjectsOfTypeAll(typeof(Font)))
                {
                    var font = candidate as Font;
                    if (font == null || !font.dynamic)
                    {
                        continue;
                    }

                    if (font.HasCharacter(Probe))
                    {
                        return font;
                    }

                    if (any == null)
                    {
                        any = font;
                    }
                }

                return any;
            }
            catch (Exception)
            {
                // Fall through.
            }

            return null;
        }

        private static Font FromOsFonts()
        {
            var names = new[]
            {
                "Noto Sans CJK SC",
                "Noto Sans SC",
                "Source Han Sans SC",
                "Source Han Sans CN",
                "WenQuanYi Micro Hei",
                "WenQuanYi Zen Hei",
                "Microsoft YaHei",
                "SimHei",
                "Droid Sans Fallback",
                "Noto Sans CJK JP",
            };

            foreach (var name in names)
            {
                try
                {
                    var font = Font.CreateDynamicFontFromOSFont(name, 14);
                    if (font != null && font.dynamic && font.HasCharacter(Probe))
                    {
                        return font;
                    }
                }
                catch (Exception)
                {
                    // Try the next name.
                }
            }

            return null;
        }
    }
}
