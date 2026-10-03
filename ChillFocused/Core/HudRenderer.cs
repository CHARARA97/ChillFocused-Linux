using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Draws the in-game overlay: the styled lines and the box behind them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Split out of the runner, which keeps the state and decides what the overlay
    /// should say; this class owns the styles alone, so the one part of the mod that is
    /// all Unity API lives in a single place. The caller supplies the layout spec, the
    /// display mode and the corner, so a hot-reload needs no state here beyond a rebuild.
    /// </para>
    /// <para>
    /// Always the last frame's measured height: the next frame places the bottom corners
    /// with it, which is what lets the panel hug its content without the caller knowing
    /// how tall it will be.
    /// </para>
    /// </remarks>
    internal sealed class HudRenderer
    {
        private readonly UiTheme _theme = new UiTheme();
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _warning;
        private GUIStyle _state;
        private GUIStyle _stateWarning;
        private Texture2D _panelTexture;
        private GUIStyle _box;
        private float _hudHeight = 150f;
        private Font _appliedFont;
        private bool _loggedFont;

        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;

        internal HudRenderer(Action<string> logInfo, Action<string> logWarning)
        {
            _logInfo = logInfo;
            _logWarning = logWarning;
        }

        /// <summary>The opaque backdrop that the settings window and toasts also borrow.</summary>
        internal Texture2D PanelTexture
        {
            get { return _panelTexture; }
        }

        /// <summary>Style for ordinary overlay lines (also the toast text).</summary>
        internal GUIStyle LabelStyle
        {
            get { return _label; }
        }

        /// <summary>Style for warning lines and warnings.</summary>
        internal GUIStyle WarningStyle
        {
            get { return _warning; }
        }

        /// <summary>Rebuild every style on the next draw; used when the layout file changes.</summary>
        internal void Reset()
        {
            _label = null;
            _title = null;
            _warning = null;
            _state = null;
            _stateWarning = null;
            _box = null;
            _loggedFont = false;
        }

        /// <summary>Release the textures this class owns. Must run on the main thread.</summary>
        internal void Destroy()
        {
            _theme.Release();

            if (_panelTexture != null)
            {
                UnityEngine.Object.Destroy(_panelTexture);
                _panelTexture = null;
            }
        }

        internal void EnsureStyles(PanelSpec spec)
        {
            // Unity's built-in IMGUI font has no CJK glyphs, so Chinese would
            // render as empty boxes. Borrow a font that does; rebuild the styles
            // if the resolved font ever changes.
            var font = HudFont.Resolve(spec.FontFamily);
            if (_label != null && ReferenceEquals(font, _appliedFont))
            {
                return;
            }

            _appliedFont = font;

            // Same theme as the settings window, so the two read as one surface:
            // same rounded panel, same colour, same corner radius.
            _theme.Refresh(spec, font);

            if (_panelTexture == null)
            {
                _panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _panelTexture.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.09f, 0.86f));
                _panelTexture.Apply();
            }

            // A vertical group styled with the panel texture: the background then
            // always matches the content, whatever the font turns out to be.
            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = _theme.PanelTexture ?? _panelTexture;
            _box.padding = new RectOffset(spec.PadOverlayLeft, spec.PadOverlayRight,
                                          spec.PadOverlayTop, spec.PadOverlayBottom);

            // A nine-slice border is what actually rounds the corners; without it the
            // texture is stretched and the overlay stays square.
            var corner = _theme.Corner;
            _box.border = new RectOffset(corner, corner, corner, corner);
            _box.margin = new RectOffset(0, 0, 0, 0);

            // Bottom padding for the same reason as the settings window: CJK glyphs
            // paint below the font's reported line height. CalcHeight includes
            // padding, so the panel grows by a couple of pixels and nothing clips.
            var breathing = new RectOffset(0, 0, 0, 3);

            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = spec.OverlayFontSize;
            _label.padding = breathing;
            _label.richText = false;
            _label.wordWrap = false;
            _label.clipping = TextClipping.Overflow;
            _label.normal.textColor = new Color(0.92f, 0.93f, 0.96f);

            _title = Derive(_label, "_title");
            _title.fontSize = spec.OverlayTitleFontSize;
            _title.padding = new RectOffset(0, 0, 0, 4);
            _title.fontStyle = FontStyle.Bold;

            _warning = Derive(_label, "_warning");
            _warning.normal.textColor = new Color(1f, 0.72f, 0.55f);

            if (font != null)
            {
                _label.font = font;
                _title.font = font;
                _warning.font = font;

                // Built here, after _label and _warning exist. Doing it earlier made
                // _state a style built from null, so it lost the font, the padding and
                // clipping = Overflow -- the line then wrapped and grew off-screen.
                // The state line gets its own size, so it can be made to stand out from the
                // rows of detail below it without changing their size at all.
                _state = Derive(_label, "_state");
                _state.fontSize = spec.OverlayStateFontSize;
                _state.padding = new RectOffset(0, 0, spec.PadStateTop, spec.PadStateBottom);
                _stateWarning = Derive(_warning ?? _label, "_stateWarning");
                _stateWarning.fontSize = spec.OverlayStateFontSize;
                _stateWarning.padding = new RectOffset(0, 0, spec.PadStateTop,
                                                       spec.PadStateBottom);
            }

            if (!_loggedFont)
            {
                _loggedFont = true;
                var fontDetail = font == null ? string.Empty : "  font: " + font.name;
                Log("overlay font resolved from " + HudFont.Source + fontDetail);
            }
        }

        /// <summary>Draw the overlay, sizing the box to whatever the lines need.</summary>
        internal void Draw(PanelSpec spec, HudMode mode, string corner, IList<string> lines)
        {
            // Width from the content rather than a constant. BeginArea hard-clips to
            // the rectangle it is given, so a fixed 300/340 cut the right-hand side
            // off as soon as the overlay font was made larger.
            var title = string.IsNullOrEmpty(spec.OverlayTitleText)
                ? "ChillFocused " + FocusPluginInfo.Version + "  [F9]"
                : spec.OverlayTitleText;

            var minimum = mode == HudMode.Minimal ? 240f : 300f;
            var measured = mode == HudMode.Minimal
                ? 0f
                : _title.CalcSize(new GUIContent(title)).x;

            for (var i = 0; i < lines.Count; i++)
            {
                var style = i == 0 ? StateStyle(lines[0]) : StyleFor(lines[i]);
                measured = Mathf.Max(measured, style.CalcSize(new GUIContent(OverlayText.TextFor(lines[i]))).x);
            }

            // Padding for the box, plus a little slack for the widest glyph.
            var available = Mathf.Max(minimum, Screen.width - spec.OverlayX * 2f);
            // Capped, because one long line -- a full exception message in the
            // 'last error' row -- otherwise stretched the panel across the screen.
            var cap = spec.OverlayMaxWidth > 0f ? spec.OverlayMaxWidth : available;
            var width = Mathf.Clamp(measured + 56f, minimum, Mathf.Min(available, cap));
            var rect = HudRect(spec, corner, width, _hudHeight);

            // Generous vertical room, then let a styled vertical group draw its own
            // background and grow to fit its content. Computing the height by hand
            // was wrong twice over: CalcHeight omits style margins (which GUILayout
            // does apply, and seven labels' worth of them is more than one row), and
            // GUILayout.BeginArea hard-clips to whatever rectangle it is handed.
            // Measuring the group afterwards removes the arithmetic entirely.
            GUILayout.BeginArea(new Rect(rect.x, rect.y, width, Mathf.Max(1f, Screen.height - rect.y)));

            GUILayout.BeginVertical(_box);

            if (mode == HudMode.Minimal)
            {
                if (lines.Count > 0)
                {
                    GUILayout.Label(FitLine(lines[0], StateStyle(lines[0]), width),
                                    StateStyle(lines[0]));
                }
            }
            else
            {
                GUILayout.Label(title, _title);
                GUILayout.Space(4f);

                for (var i = 0; i < lines.Count; i++)
                {
                    GUILayout.Label(FitLine(lines[i], StyleFor(lines[i]), width),
                                    StyleFor(lines[i]));
                }
            }

            GUILayout.EndVertical();

            var used = GUILayoutUtility.GetLastRect();
            if (used.height > 1f && Mathf.Abs(used.height - _hudHeight) > 0.5f)
            {
                // Learned for the next frame, which is what lets the bottom corners
                // place themselves without knowing the height in advance.
                _hudHeight = Mathf.Min(used.height, Screen.height);
            }

            GUILayout.EndArea();
        }

        /// <summary>The state line's style, keeping the warning colour when idle.</summary>
        private GUIStyle StateStyle(string line)
        {
            var warning = line.StartsWith("!", StringComparison.Ordinal);
            return warning && _stateWarning != null ? _stateWarning : (_state ?? _label);
        }

        /// <summary>
        /// The line's text, trimmed to the panel's inner width.
        /// </summary>
        /// <remarks>
        /// Every line, not just the error row: the width cap stops the panel from growing,
        /// and Overflow clipping then lets anything longer than the cap spill outside the
        /// box instead of being cut. Doing this on the way to the label is what keeps a
        /// long process name inside the panel.
        /// </remarks>
        private string FitLine(string line, GUIStyle style, float panelWidth)
        {
            return TextFit.Fit(OverlayText.TextFor(line), panelWidth - 44f,
                               delegate(string candidate)
                               {
                                   return style.CalcSize(new GUIContent(candidate)).x;
                               });
        }

        /// <summary>
        /// Copy a style, refusing to build from a missing one.
        /// </summary>
        /// <remarks>
        /// <c>new GUIStyle(null)</c> compiles and yields an empty style: the font, the
        /// padding and <c>clipping = Overflow</c> all vanish. One of these was created
        /// before its base existed, and the overlay then wrapped its single line and grew
        /// off the bottom of the screen. The compiler cannot see this; a null check can.
        /// </remarks>
        private GUIStyle Derive(GUIStyle basis, string name)
        {
            if (basis != null)
            {
                return new GUIStyle(basis);
            }

            LogWarning("style '" + name + "' was built before its base existed; using the default");
            return new GUIStyle(GUI.skin.label);
        }

        private GUIStyle StyleFor(string line)
        {
            return line.StartsWith("!", StringComparison.Ordinal) ? _warning : _label;
        }

        private Rect HudRect(PanelSpec spec, string corner, float width, float height)
        {
            // From the spec, so the position can be hot-reloaded. The default y clears
            // the game's own date and clock in the top-left corner.
            var anchor = OverlayLayout.AnchorFor(corner, width, height,
                                                 Screen.width, Screen.height,
                                                 spec.OverlayX, spec.OverlayY);
            return new Rect(anchor.x, anchor.y, width, height);
        }

        private void Log(string message)
        {
            if (_logInfo != null)
            {
                _logInfo(message);
            }
        }

        private void LogWarning(string message)
        {
            if (_logWarning != null)
            {
                _logWarning(message);
            }
        }
    }
}
