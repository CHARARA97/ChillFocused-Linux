using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChillFocused.Core
{
    /// <summary>
    /// Layout and preview settings for the in-game panel, loaded from a local file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Editing the panel used to mean rebuilding the plugin and restarting the game,
    /// which is a slow loop for what is almost always a question of pixels. This file
    /// is read live, so a change shows up on the next poll.
    /// </para>
    /// <para>
    /// The format is deliberately <c>key = value</c> rather than JSON. This file is
    /// meant to be edited by hand: comments are allowed, there is no escaping to get
    /// wrong, and parsing it cannot silently produce a null array the way Unity's
    /// JsonUtility did for the process list.
    /// </para>
    /// <para>
    /// It controls appearance and preview data only. It is not a widget tree: letting
    /// a file decide what controls exist would be a much larger surface for no
    /// debugging benefit.
    /// </para>
    /// </remarks>
    public sealed class PanelSpec
    {
        // -- window ------------------------------------------------------------

        /// <summary>Where the window starts. Kept clear of the overlay.</summary>
        /// <summary>Where the overlay sits. Below the game's own clock by default.</summary>
        public float OverlayX = 20f;
        public float OverlayY = 215f;

        /// <summary>Width cap for the overlay. 0 means "as wide as needed".</summary>
        public float OverlayMaxWidth = 420f;

        public float WindowX = 400f;   // the overlay occupies 20..360
        public float WindowY = 215f;   // level with the overlay, clear of the clock
        public float WindowWidth = 640f;
        public float WindowHeight = 700f;

        // -- text --------------------------------------------------------------

        /// <summary>Preferred font name; empty means auto-detect.</summary>
        public string FontFamily = string.Empty;

        public int TitleFontSize = 18;
        public int HeadlineFontSize = 18;
        public int SectionFontSize = 17;
        public int BodyFontSize = 15;
        public int RowFontSize = 15;
        public int ButtonFontSize = 13;
        public int HintFontSize = 13;
        public int FieldFontSize = 15;

        /// <summary>Font size of the overlay in the top-left corner.</summary>
        public int OverlayFontSize = 12;
        public int OverlayTitleFontSize = 13;

        /// <summary>Size of the state line: the '创作模式' / '待机中' row.</summary>
        public int OverlayStateFontSize = 15;

        // -- text overrides. Empty keeps the built-in string --------------------

        public string TitleText = string.Empty;
        public string OverlayTitleText = string.Empty;
        public string CloseText = string.Empty;
        public string AdvancedText = string.Empty;
        public string BackText = string.Empty;

        // -- spacing, in pixels ------------------------------------------------

        public int PadLabelBottom = 3;
        public int PadRowTop = 2;
        public int PadRowBottom = 4;
        public int PadButtonLeft = 8;
        public int PadButtonRight = 8;
        public int PadButtonTop = 2;
        public int PadButtonBottom = 4;

        /// <summary>Space above and below the state line; equal values centre it.</summary>
        public int PadStateTop = 6;
        public int PadStateBottom = 6;

        /// <summary>Inner padding of the overlay panel, in pixels.</summary>
        public int PadOverlayLeft = 20;
        public int PadOverlayRight = 20;
        public int PadOverlayTop = 16;
        public int PadOverlayBottom = 16;

        // -- lists, in pixels --------------------------------------------------

        public float BlockedListMaxHeight = 170f;
        public float PickerListMaxHeight = 250f;

        // -- colours, as RRGGBB or RRGGBBAA ------------------------------------

        public string PanelColor = "0a0d17db";   // the overlay's own colour
        public string BorderColor = "2b3346";
        public string TextColor = "e9ebf2";
        public string HintColor = "98a2b6";
        public string AccentColor = "7aa2f7";
        /// <summary>Colour of an app that is already on the blacklist.</summary>
        public string BlockedColor = "c05a55";
        public string ButtonColor = "1b2029";
        public string ButtonHoverColor = "272e3b";
        public string FieldColor = "0e1116";

        /// <summary>Corner radius, in pixels. 0 gives square corners.</summary>
        public int Radius = 6;

        // -- preview data ------------------------------------------------------

        /// <summary>Above zero, the blocked list is filled with this many fake names.</summary>
        /// <summary>Open the advanced view as soon as the window appears.</summary>
        public int StartAdvanced;

        /// <summary>Hide protected processes from the running list. 0 / 1</summary>
        public int HideProtected = 1;

        /// <summary>Recolour the scrollbar thumb. 0 restores the stock bar.</summary>
        // Off by default. Three attempts to restyle the scrollbar each broke it in a
        // different way (bar vanished, thumb invisible), and the stock bar works. The
        // key stays so the experiment is one line away, not a rebuild.
        public int ScrollbarThemed;

        public int PreviewApps;

        /// <summary>Above zero, the picker is filled with this many fake processes.</summary>
        public int PreviewProcesses;

        /// <summary>Where this was loaded from, for the panel to display.</summary>
        public string Source = string.Empty;

        public bool Previewing
        {
            get { return PreviewApps > 0 || PreviewProcesses > 0; }
        }

        public static PanelSpec Defaults()
        {
            return new PanelSpec();
        }

        /// <summary>
        /// The file written on first run: every key, with the ranges in comments.
        /// </summary>
        public static string Template()
        {
            var text = new StringBuilder();
            text.AppendLine("# ChillFocused 面板布局");
            text.AppendLine("#");
            text.AppendLine("# 保存后 1 秒内生效，无需重启游戏。");
            text.AppendLine("# 数值超出范围会收进合法区间并在日志中说明；文本键留空表示使用内置文字。");
            text.AppendLine();
            text.AppendLine("# --- 位置 ----------------------------------------------------");
            text.AppendLine("# 状态面板与设置窗口的位置、尺寸。y 用于避让游戏自带的时间显示。");
            text.AppendLine("overlay.x = 5");
            text.AppendLine("overlay.y = 5");
            text.AppendLine("overlay.width.max = 420");
            text.AppendLine("window.x = 400");
            text.AppendLine("window.y = 215");
            text.AppendLine("window.width = 640");
            text.AppendLine("window.height = 700");
            text.AppendLine();
            text.AppendLine("# --- 状态面板 ------------------------------------------------");
            text.AppendLine("# 左上角信息面板的字体与内边距。");
            text.AppendLine("font.overlay = 12");
            text.AppendLine("font.overlay.title = 13");
            text.AppendLine("font.overlay.state = 15");
            text.AppendLine("pad.overlay.left = 20");
            text.AppendLine("pad.overlay.right = 20");
            text.AppendLine("pad.overlay.top = 16");
            text.AppendLine("pad.overlay.bottom = 16");
            text.AppendLine("pad.state.top = 6");
            text.AppendLine("pad.state.bottom = 6");
            text.AppendLine();
            text.AppendLine("# --- 设置面板字体 --------------------------------------------");
            text.AppendLine("# font.family 留空时自动挑选系统字体。");
            text.AppendLine("font.family = ");
            text.AppendLine("font.title = 18");
            text.AppendLine("font.headline = 18");
            text.AppendLine("font.section = 17");
            text.AppendLine("font.body = 15");
            text.AppendLine("font.row = 15");
            text.AppendLine("font.button = 13");
            text.AppendLine("font.hint = 13");
            text.AppendLine("font.field = 15");
            text.AppendLine();
            text.AppendLine("# --- 设置面板内边距 ------------------------------------------");
            text.AppendLine("# 各行与按钮的留白。");
            text.AppendLine("pad.label.bottom = 3");
            text.AppendLine("pad.row.top = 2");
            text.AppendLine("pad.row.bottom = 4");
            text.AppendLine("pad.button.left = 8");
            text.AppendLine("pad.button.right = 8");
            text.AppendLine("pad.button.top = 2");
            text.AppendLine("pad.button.bottom = 4");
            text.AppendLine();
            text.AppendLine("# --- 文本 ----------------------------------------------------");
            text.AppendLine("# 留空表示使用内置文字。");
            text.AppendLine("text.title = ");
            text.AppendLine("text.overlay.title = ");
            text.AppendLine("text.close = ");
            text.AppendLine("text.advanced = ");
            text.AppendLine("text.back = ");
            text.AppendLine();
            text.AppendLine("# --- 列表 ----------------------------------------------------");
            text.AppendLine("# 高度上限与过滤行为。");
            text.AppendLine("list.blocked.max = 170");
            text.AppendLine("list.picker.max = 250");
            text.AppendLine("list.picker.hide_protected = 1");
            text.AppendLine();
            text.AppendLine("# --- 外观 ----------------------------------------------------");
            text.AppendLine("# 配色为 RRGGBB 或 RRGGBBAA；radius 为圆角半径。");
            text.AppendLine("color.panel = 0a0d17db");
            text.AppendLine("color.border = 2b3346");
            text.AppendLine("color.text = e9ebf2");
            text.AppendLine("color.hint = 98a2b6");
            text.AppendLine("color.accent = 7aa2f7");
            text.AppendLine("color.blocked = c05a55");
            text.AppendLine("color.button = 1b2029");
            text.AppendLine("color.button.hover = 272e3b");
            text.AppendLine("color.field = 0e1116");
            text.AppendLine("radius = 8");
            text.AppendLine("scrollbar.themed = 0");
            text.AppendLine();
            text.AppendLine("# --- 调试 ----------------------------------------------------");
            text.AppendLine("# 排查排版问题时使用，正常游玩保持默认。");
            text.AppendLine("panel.advanced = 0");
            text.AppendLine("preview.apps = 0");
            text.AppendLine("preview.processes = 0");
            text.AppendLine();
            return text.ToString();
        }

        private const float WindowMin = 320f;
        private const float WindowMax = 4000f;
        private const int FontMin = 8;
        private const int FontMax = 48;
        private const int PadMin = 0;
        private const int PadMax = 40;
        private const float ListMin = 40f;
        private const float ListMax = 2000f;
        private const int PreviewMax = 500;

        /// <summary>
        /// Parse <c>key = value</c> lines over the defaults.
        /// </summary>
        /// <remarks>
        /// Unknown keys and unparseable values are collected rather than thrown: a
        /// typo in a debug file should not cost a restart, and the reason belongs in
        /// the log.
        /// </remarks>
        public void Apply(string text, List<string> problems)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    Note(problems, "line " + (i + 1) + ": expected 'key = value', got '" +
                                   Clip(line) + "'");
                    continue;
                }

                var key = line.Substring(0, split).Trim().ToLowerInvariant();
                var value = line.Substring(split + 1).Trim();
                if (!Set(key, value))
                {
                    Note(problems, "line " + (i + 1) + ": unknown key '" + key + "'");
                }
            }
        }

        private bool Set(string key, string value)
        {
            float number;
            switch (key)
            {
                case "overlay.x": return Float(value, out number, v => OverlayX = v);
                case "overlay.y": return Float(value, out number, v => OverlayY = v);
                case "overlay.width.max":
                    return Float(value, out number, v => OverlayMaxWidth = v);
                case "window.x": return Float(value, out number, v => WindowX = v);
                case "window.y": return Float(value, out number, v => WindowY = v);
                case "panel.advanced":
                    return Int(value, out number, v => StartAdvanced = (int)v);
                case "window.width": return Float(value, out number, v => WindowWidth = v);
                case "window.height": return Float(value, out number, v => WindowHeight = v);

                case "font.family": return Text(value, v => FontFamily = v);
                case "font.title": return Int(value, out number, v => TitleFontSize = (int)v);
                case "font.headline":
                    return Int(value, out number, v => HeadlineFontSize = (int)v);
                case "font.row": return Int(value, out number, v => RowFontSize = (int)v);
                case "font.button": return Int(value, out number, v => ButtonFontSize = (int)v);
                case "font.field": return Int(value, out number, v => FieldFontSize = (int)v);
                case "font.overlay":
                    return Int(value, out number, v => OverlayFontSize = (int)v);
                case "font.overlay.state":
                    return Int(value, out number, v => OverlayStateFontSize = (int)v);
                case "font.overlay.title":
                    return Int(value, out number, v => OverlayTitleFontSize = (int)v);
                case "text.title": return Text(value, v => TitleText = v);
                case "text.overlay.title": return Text(value, v => OverlayTitleText = v);
                case "text.close": return Text(value, v => CloseText = v);
                case "text.advanced": return Text(value, v => AdvancedText = v);
                case "text.back": return Text(value, v => BackText = v);
                case "font.section": return Int(value, out number, v => SectionFontSize = (int)v);
                case "font.body": return Int(value, out number, v => BodyFontSize = (int)v);
                case "font.hint": return Int(value, out number, v => HintFontSize = (int)v);

                case "pad.label.bottom": return Int(value, out number, v => PadLabelBottom = (int)v);
                case "pad.row.top": return Int(value, out number, v => PadRowTop = (int)v);
                case "pad.row.bottom": return Int(value, out number, v => PadRowBottom = (int)v);
                case "pad.button.left": return Int(value, out number, v => PadButtonLeft = (int)v);
                case "pad.button.right": return Int(value, out number, v => PadButtonRight = (int)v);
                case "pad.button.top": return Int(value, out number, v => PadButtonTop = (int)v);
                case "pad.overlay.left":
                    return Int(value, out number, v => PadOverlayLeft = (int)v);
                case "pad.overlay.right":
                    return Int(value, out number, v => PadOverlayRight = (int)v);
                case "pad.overlay.top":
                    return Int(value, out number, v => PadOverlayTop = (int)v);
                case "pad.overlay.bottom":
                    return Int(value, out number, v => PadOverlayBottom = (int)v);
                case "pad.state.top": return Int(value, out number, v => PadStateTop = (int)v);
                case "pad.state.bottom":
                    return Int(value, out number, v => PadStateBottom = (int)v);
                case "pad.button.bottom": return Int(value, out number, v => PadButtonBottom = (int)v);

                case "list.blocked.max":
                    return Float(value, out number, v => BlockedListMaxHeight = v);
                case "list.picker.max":
                    return Float(value, out number, v => PickerListMaxHeight = v);

                case "color.panel": return Text(value, v => PanelColor = v);
                case "color.border": return Text(value, v => BorderColor = v);
                case "color.text": return Text(value, v => TextColor = v);
                case "color.hint": return Text(value, v => HintColor = v);
                case "color.accent": return Text(value, v => AccentColor = v);
                case "color.blocked": return Text(value, v => BlockedColor = v);
                case "color.button": return Text(value, v => ButtonColor = v);
                case "color.button.hover": return Text(value, v => ButtonHoverColor = v);
                case "color.field": return Text(value, v => FieldColor = v);
                case "radius": return Int(value, out number, v => Radius = (int)v);

                case "list.picker.hide_protected":
                    return Int(value, out number, v => HideProtected = (int)v);
                case "scrollbar.themed":
                    return Int(value, out number, v => ScrollbarThemed = (int)v);
                case "preview.apps": return Int(value, out number, v => PreviewApps = (int)v);
                case "preview.processes":
                    return Int(value, out number, v => PreviewProcesses = (int)v);

                default:
                    return false;
            }
        }

        private static bool Float(string value, out float parsed, Action<float> assign)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                assign(parsed);
                return true;
            }

            return false;
        }

        private static bool Text(string value, Action<string> assign)
        {
            // An empty value is valid, and means "use the built-in": that is what
            // `font.family =` and the `text.*` overrides look like in the template.
            assign((value ?? string.Empty).Trim());
            return true;
        }

        private static bool Int(string value, out float parsed, Action<float> assign)
        {
            return Float(value, out parsed, assign);
        }

        /// <summary>
        /// Clamp everything into a range the panel can survive, reporting what moved.
        /// </summary>
        public void Clamp(List<string> problems)
        {
            WindowWidth = Bounded(problems, "window.width", WindowWidth, WindowMin, WindowMax);
            WindowHeight = Bounded(problems, "window.height", WindowHeight, WindowMin, WindowMax);

            TitleFontSize = (int)Bounded(problems, "font.title", TitleFontSize, FontMin, FontMax);
            HeadlineFontSize = (int)Bounded(problems, "font.headline", HeadlineFontSize,
                                            FontMin, FontMax);
            RowFontSize = (int)Bounded(problems, "font.row", RowFontSize, FontMin, FontMax);
            ButtonFontSize = (int)Bounded(problems, "font.button", ButtonFontSize, FontMin, FontMax);
            FieldFontSize = (int)Bounded(problems, "font.field", FieldFontSize, FontMin, FontMax);
            OverlayFontSize = (int)Bounded(problems, "font.overlay", OverlayFontSize,
                                           FontMin, FontMax);
            OverlayTitleFontSize = (int)Bounded(problems, "font.overlay.title",
                                                OverlayTitleFontSize, FontMin, FontMax);
            OverlayStateFontSize = (int)Bounded(problems, "font.overlay.state",
                                                OverlayStateFontSize, FontMin, FontMax);
            PadOverlayLeft = (int)Bounded(problems, "pad.overlay.left", PadOverlayLeft, 0, 60);
            PadOverlayRight = (int)Bounded(problems, "pad.overlay.right", PadOverlayRight, 0, 60);
            PadOverlayTop = (int)Bounded(problems, "pad.overlay.top", PadOverlayTop, 0, 60);
            PadOverlayBottom = (int)Bounded(problems, "pad.overlay.bottom",
                                            PadOverlayBottom, 0, 60);
            PadStateTop = (int)Bounded(problems, "pad.state.top", PadStateTop, 0, 40);
            PadStateBottom = (int)Bounded(problems, "pad.state.bottom", PadStateBottom, 0, 40);
            SectionFontSize = (int)Bounded(problems, "font.section", SectionFontSize, FontMin, FontMax);
            BodyFontSize = (int)Bounded(problems, "font.body", BodyFontSize, FontMin, FontMax);
            HintFontSize = (int)Bounded(problems, "font.hint", HintFontSize, FontMin, FontMax);

            PadLabelBottom = (int)Bounded(problems, "pad.label.bottom", PadLabelBottom, PadMin, PadMax);
            PadRowTop = (int)Bounded(problems, "pad.row.top", PadRowTop, PadMin, PadMax);
            PadRowBottom = (int)Bounded(problems, "pad.row.bottom", PadRowBottom, PadMin, PadMax);
            PadButtonLeft = (int)Bounded(problems, "pad.button.left", PadButtonLeft, PadMin, PadMax);
            PadButtonRight = (int)Bounded(problems, "pad.button.right", PadButtonRight, PadMin, PadMax);
            PadButtonTop = (int)Bounded(problems, "pad.button.top", PadButtonTop, PadMin, PadMax);
            PadButtonBottom = (int)Bounded(problems, "pad.button.bottom", PadButtonBottom, PadMin, PadMax);

            BlockedListMaxHeight = Bounded(problems, "list.blocked.max", BlockedListMaxHeight,
                                           ListMin, ListMax);
            PickerListMaxHeight = Bounded(problems, "list.picker.max", PickerListMaxHeight,
                                          ListMin, ListMax);

            Radius = (int)Bounded(problems, "radius", Radius, 0, 40);
            OverlayX = Bounded(problems, "overlay.x", OverlayX, 0f, WindowMax);
            OverlayY = Bounded(problems, "overlay.y", OverlayY, 0f, WindowMax);
            OverlayMaxWidth = Bounded(problems, "overlay.width.max", OverlayMaxWidth,
                                      0f, WindowMax);
            WindowX = Bounded(problems, "window.x", WindowX, 0f, WindowMax);
            WindowY = Bounded(problems, "window.y", WindowY, 0f, WindowMax);
            StartAdvanced = (int)Bounded(problems, "panel.advanced", StartAdvanced, 0, 1);
            CheckColor(problems, "color.panel", PanelColor);
            CheckColor(problems, "color.border", BorderColor);
            CheckColor(problems, "color.text", TextColor);
            CheckColor(problems, "color.hint", HintColor);
            CheckColor(problems, "color.accent", AccentColor);
            CheckColor(problems, "color.blocked", BlockedColor);
            CheckColor(problems, "color.button", ButtonColor);
            CheckColor(problems, "color.button.hover", ButtonHoverColor);
            CheckColor(problems, "color.field", FieldColor);

            HideProtected = (int)Bounded(problems, "list.picker.hide_protected",
                                         HideProtected, 0, 1);
            ScrollbarThemed = (int)Bounded(problems, "scrollbar.themed", ScrollbarThemed, 0, 1);
            PreviewApps = (int)Bounded(problems, "preview.apps", PreviewApps, 0, PreviewMax);
            PreviewProcesses = (int)Bounded(problems, "preview.processes", PreviewProcesses,
                                            0, PreviewMax);
        }

        private static float Bounded(List<string> problems, string key, float value,
                                     float low, float high)
        {
            if (value < low)
            {
                Note(problems, key + " = " + Show(value) + " raised to " + Show(low));
                return low;
            }

            if (value > high)
            {
                Note(problems, key + " = " + Show(value) + " lowered to " + Show(high));
                return high;
            }

            return value;
        }

        /// <summary>Deterministic fake names, so a preview looks the same each run.</summary>
        public string[] PreviewNames()
        {
            var pool = new[]
            {
                "firefox", "chromium", "vivaldi-bin", "discord", "slack", "qq",
                "telegram-desktop", "spotify", "steam", "code", "obs", "mpv",
                "thunderbird", "libreoffice", "gimp", "inkscape", "blender", "godot",
                "java", "node", "python3", "docker", "syncthing", "kdeconnectd",
            };

            var names = new string[PreviewApps];
            for (var i = 0; i < PreviewApps; i++)
            {
                names[i] = pool[i % pool.Length] + (i >= pool.Length ? "-" + (i / pool.Length) : string.Empty);
            }

            return names;
        }

        /// <summary>Deterministic fake processes, for checking a long list.</summary>
        public ProcessItem[] PreviewItems()
        {
            var names = PreviewNames();
            var count = Math.Max(PreviewProcesses, 0);
            var items = new ProcessItem[count];
            for (var i = 0; i < count; i++)
            {
                var protectedRow = i % 9 == 8;
                items[i] = new ProcessItem
                {
                    name = names.Length > 0
                        ? names[i % names.Length]
                        : "process-" + i,
                    count = 1 + (i % 5),
                    @protected = protectedRow,
                    protect_rule = protectedRow ? "preview" : string.Empty,
                };
            }

            return items;
        }

        /// <summary>At least six hex digits, optionally two more for alpha.</summary>
        private static void CheckColor(List<string> problems, string key, string value)
        {
            var text = (value ?? string.Empty).TrimStart('#');
            var ok = text.Length == 6 || text.Length == 8;
            if (ok)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    if (!Uri.IsHexDigit(text[i]))
                    {
                        ok = false;
                        break;
                    }
                }
            }

            if (!ok)
            {
                Note(problems, key + " = '" + value + "' is not RRGGBB or RRGGBBAA; using defaults");
            }
        }

        private static void Note(List<string> problems, string message)
        {
            if (problems != null && problems.Count < 40)
            {
                problems.Add(message);
            }
        }

        private static string Show(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Clip(string value)
        {
            return value.Length <= 40 ? value : value.Substring(0, 40) + "…";
        }
    }
}
