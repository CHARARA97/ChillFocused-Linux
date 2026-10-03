using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Builds the panel's look from the hot-reloaded spec.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's default IMGUI skin is light grey and square, which fights a dark
    /// translucent overlay: the first version of this panel had grey buttons and
    /// grey scrollbars sitting inside a near-black window. Everything here exists
    /// to replace that.
    /// </para>
    /// <para>
    /// Rounded rectangles are drawn into small textures and nine-sliced via
    /// <see cref="GUIStyle.border"/>, which is the only way to get rounded corners
    /// out of IMGUI. The textures are rebuilt only when the spec changes, so a
    /// hot-reload costs one rebuild rather than one per frame.
    /// </para>
    /// </remarks>
    internal sealed class UiTheme
    {
        // -- palette -----------------------------------------------------------

        internal Color Panel = new Color(0.04f, 0.05f, 0.09f, 0.86f);   // same as the overlay
        internal Color Border = new Color(0.17f, 0.20f, 0.27f, 1f);
        internal Color Text = new Color(0.91f, 0.92f, 0.95f, 1f);
        internal Color Hint = new Color(0.60f, 0.64f, 0.71f, 1f);
        internal Color Accent = new Color(0.48f, 0.64f, 0.97f, 1f);
        internal Color Blocked = new Color(0.75f, 0.35f, 0.33f, 1f);
        internal Color Button = new Color(0.11f, 0.13f, 0.16f, 1f);
        internal Color ButtonHover = new Color(0.15f, 0.18f, 0.23f, 1f);
        internal Color Field = new Color(0.05f, 0.07f, 0.09f, 1f);
        internal int Radius = 6;

        // -- generated resources ------------------------------------------------

        private Texture2D _panel;
        private Texture2D _button;
        private Texture2D _buttonHover;
        private Texture2D _field;
        private Texture2D _header;
        private Texture2D _line;
        internal Texture2D CheckboxOn;
        internal Texture2D CheckboxOff;
        private Font _font;

        /// <summary>The rounded panel texture, shared with the overlay.</summary>
        internal Texture2D PanelTexture { get { return _panel; } }

        /// <summary>Nine-slice inset matching the corner radius.</summary>
        internal int Corner { get { return Mathf.Clamp(Radius + 1, 1, 21); } }

        internal GUIStyle Window;
        internal GUIStyle Headline;
        internal GUIStyle Section;
        internal GUIStyle Body;
        internal GUIStyle HintStyle;
        internal GUIStyle Row;
        internal GUIStyle RowBlocked;
        internal GUIStyle Header;
        internal GUIStyle ButtonStyle;
        internal GUIStyle FieldStyle;

        private string _signature = string.Empty;
        private bool _loggedDiagnostics;
        private GUIStyle _defaultVerticalBar;
        private GUIStyle _defaultHorizontalBar;
        private GUIStyle _defaultVerticalThumb;
        private GUIStyle _defaultHorizontalThumb;

        /// <summary>Rebuild everything when the spec (or the font) changes.</summary>
        internal void Refresh(PanelSpec spec, Font font)
        {
            var titleFont = spec.TitleFontSize;
            var headlineFont = spec.HeadlineFontSize;
            var sectionFont = spec.SectionFontSize;
            var bodyFont = spec.BodyFontSize;
            var rowFont = spec.RowFontSize;
            var buttonFont = spec.ButtonFontSize;
            var hintFont = spec.HintFontSize;
            var fieldFont = spec.FieldFontSize;

            var signature = string.Join("|", new[]
            {
                spec.PanelColor, spec.BorderColor, spec.TextColor, spec.HintColor,
                spec.AccentColor, spec.BlockedColor, spec.ButtonColor, spec.ButtonHoverColor, spec.FieldColor,
                spec.Radius.ToString(), spec.ScrollbarThemed.ToString(),
                bodyFont.ToString(), hintFont.ToString(),
                sectionFont.ToString(), titleFont.ToString(), headlineFont.ToString(),
                rowFont.ToString(), buttonFont.ToString(), fieldFont.ToString(),
                font == null ? "?" : font.name,
            });

            var sameFont = ReferenceEquals(font, _font);
            if (signature == _signature && sameFont)
            {
                return;
            }

            _signature = signature;
            _font = font;

            ReadPalette(spec);
            // Deliberately not destroying the previous textures: the styles that
            // reference them may still be assigned to GUI.skin for the rest of this
            // frame, and a destroyed texture draws as a flat grey block.

            _panel = Rounded(Radius, Panel, Border);
            _button = Rounded(Radius, Button, Border);
            _buttonHover = Rounded(Radius, ButtonHover, Accent);
            _field = Rounded(Radius, Field, Border);
            _header = Rounded(Radius, Button, Border);
            _line = Solid(new Color(Border.r, Border.g, Border.b, 0.65f));

            // Drawn here rather than left to the built-in skin, whose checkboxes
            // are flat light-grey squares that look nothing like the rest.
            CheckboxOff = CheckboxTexture(18, false, Field, Border, Accent);
            CheckboxOn = CheckboxTexture(18, true, Field, Border, Color.white);

            if (!_loggedDiagnostics && _panel != null)
            {
                _loggedDiagnostics = true;
                // Sampled from the generated texture itself, because the geometry
                // cannot be verified outside Unity: a corner pixel must be
                // transparent and the centre opaque, or the nine-slice smears.
                var corner = _panel.GetPixel(0, 0);
                var centre = _panel.GetPixel(_panel.width / 2, _panel.height / 2);
                Debug.Log(string.Format(
                    "[ChillFocused] theme: panel {0}x{1} radius={2} " +
                    "corner a={3:0.00} centre a={4:0.00} rgb={5:0.00},{6:0.00},{7:0.00}",
                    _panel.width, _panel.height, Radius,
                    corner.a, centre.a, centre.r, centre.g, centre.b));
            }

            Window = Style(GUI.skin.window, titleFont, _panel, _panel, Padding(20, 20, 38, 18));
            Headline = Style(GUI.skin.label, headlineFont, null, null, Padding(2, 2, 2, 6));
            Section = Style(GUI.skin.label, sectionFont, null, null, Padding(0, 0, 0, 4));
            Section.fontStyle = FontStyle.Bold;
            Body = Style(GUI.skin.label, bodyFont, null, null, Padding(0, 0, 0, spec.PadLabelBottom));
            // Symmetric vertical padding, and no top margin: IMGUI lays a horizontal
            // row out using each style's margin.top, so a bottom-only padding left
            // hints sitting higher than the buttons beside them.
            HintStyle = Style(GUI.skin.label, hintFont, null, null,
                              Padding(0, 0, spec.PadLabelBottom, spec.PadLabelBottom));
            HintStyle.wordWrap = true;
            // Left padding zero, so rows and checkbox labels line up with the section
            // headings and hints instead of sitting 8px further in than everything else.
            Row = Style(GUI.skin.label, rowFont, null, null,
                        new RectOffset(0, 8, spec.PadRowTop, spec.PadRowBottom));
            Row.margin = new RectOffset(0, 0, 0, 0);
            Row.alignment = TextAnchor.MiddleLeft;
            // Already-blocked rows are shown in dark red rather than with a tick, so the
            // list reads at a glance without a prefix shifting every name right.
            RowBlocked = new GUIStyle(Row);

            Header = Style(GUI.skin.button, sectionFont, _header, _header, Padding(12, 12, 6, 8));
            Header.alignment = TextAnchor.MiddleLeft;
            ButtonStyle = Style(GUI.skin.button, buttonFont, _button, _buttonHover,
                                  Padding(14, 14, 6, 6));
            ButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            FieldStyle = Style(GUI.skin.textField, fieldFont, _field, _field,
                               Padding(8, 8, 5, 5));
            FieldStyle.margin = new RectOffset(0, 0, 0, 0);

            // Explicit, because the built-in skin's own colours are chosen for a
            // light background and would put dark text on this dark panel.
            SetTextColors(Window, Text);
            SetTextColors(Headline, Text);
            SetTextColors(Section, Accent);
            SetTextColors(Body, Text);
            SetTextColors(HintStyle, Hint);
            SetTextColors(Row, Text);
            SetTextColors(RowBlocked, Blocked);
            SetTextColors(Header, Text);
            SetTextColors(ButtonStyle, Text);
            SetTextColors(FieldStyle, Text);

            GUI.skin.label = Body;
            GUI.skin.button = ButtonStyle;
            GUI.skin.textField = FieldStyle;

            // The checkbox image is the built-in skin's, which is fine on dark;
            // only the label colour needs pinning.
            GUI.skin.toggle.fontSize = bodyFont;
            GUI.skin.toggle.padding = new RectOffset(
                GUI.skin.toggle.padding.left, GUI.skin.toggle.padding.right, 2, 4);
            if (_font != null)
            {
                GUI.skin.toggle.font = _font;
            }

            SetTextColors(GUI.skin.toggle, Text);
            StyleScrollbars(spec.ScrollbarThemed > 0);
        }

        private void ReadPalette(PanelSpec spec)
        {
            Panel = Parse(spec.PanelColor, Panel);
            Border = Parse(spec.BorderColor, Border);
            Text = Parse(spec.TextColor, Text);
            Hint = Parse(spec.HintColor, Hint);
            Accent = Parse(spec.AccentColor, Accent);
            Blocked = Parse(spec.BlockedColor, Blocked);
            Button = Parse(spec.ButtonColor, Button);
            ButtonHover = Parse(spec.ButtonHoverColor, ButtonHover);
            Field = Parse(spec.FieldColor, Field);
            Radius = Mathf.Clamp(spec.Radius, 0, 40);
        }

        private static Color Parse(string value, Color fallback)
        {
            Color parsed;
            var text = (value ?? string.Empty).Trim();
            if (text.Length > 0 && text[0] != '#')
            {
                text = "#" + text;
            }

            return ColorUtility.TryParseHtmlString(text, out parsed) ? parsed : fallback;
        }

        private GUIStyle Style(GUIStyle basis, int fontSize, Texture2D normal, Texture2D hover,
                               RectOffset padding)
        {
            var style = new GUIStyle(basis);
            style.fontSize = fontSize;
            style.padding = padding;
            style.clipping = TextClipping.Overflow;
            if (_font != null)
            {
                style.font = _font;
            }

            // Every state, not just normal: an unset state falls back to the built-in
            // skin, which is how the window once turned grey and the title once turned
            // black on hover.
            if (normal != null)
            {
                SetBackgrounds(style, normal, hover ?? normal);
            }

            return style;
        }

        private void SetBackgrounds(GUIStyle style, Texture2D normal, Texture2D hover)
        {
            // The slice border has to match the texture's corner size. Inheriting the
            // base style's border (Unity's button/window borders are much larger than
            // this texture) stretched the corners across the whole control and smeared
            // it into a flat grey block.
            var corner = Mathf.Clamp(Radius + 1, 1, 21);
            var border = new RectOffset(corner, corner, corner, corner);

            style.normal.background = normal;
            style.onNormal.background = normal;
            style.focused.background = normal;
            style.onFocused.background = normal;
            style.hover.background = hover;
            style.onHover.background = hover;
            style.active.background = hover;
            style.onActive.background = hover;
            style.border = border;

            SetTextColors(style, style.normal.textColor == default(Color)
                ? Color.white
                : style.normal.textColor);
        }

        /// <summary>One colour for all eight states, so hovering cannot recolour text.</summary>
        internal static void SetTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        /// <summary>
        /// Recolour the scrollbars, or put the originals back.
        /// </summary>
        /// <remarks>
        /// Only the thumb's colour is touched. An earlier attempt also replaced the
        /// track and the thumb's nine-slice border, and the scrollbar then stopped
        /// drawing altogether, so this stays deliberately minimal. The copies taken
        /// on the first call are what makes `scrollbar.themed = 0` able to restore
        /// the stock bar at runtime rather than needing a rebuild.
        /// </remarks>
        internal void StyleScrollbars(bool themed)
        {
            if (_defaultVerticalBar == null)
            {
                _defaultVerticalBar = new GUIStyle(GUI.skin.verticalScrollbar);
                _defaultHorizontalBar = new GUIStyle(GUI.skin.horizontalScrollbar);
                _defaultVerticalThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb);
                _defaultHorizontalThumb = new GUIStyle(GUI.skin.horizontalScrollbarThumb);
            }

            if (!themed)
            {
                GUI.skin.verticalScrollbar = _defaultVerticalBar;
                GUI.skin.horizontalScrollbar = _defaultHorizontalBar;
                GUI.skin.verticalScrollbarThumb = _defaultVerticalThumb;
                GUI.skin.horizontalScrollbarThumb = _defaultHorizontalThumb;
                return;
            }

            // The thumb only. Styling GUI.skin.verticalScrollbar's background -- with or
            // without a nine-slice border -- stops the whole bar from drawing, observed
            // twice: first with a rounded track texture, then again after rebuilding it.
            // The stock track keeps its look; scrollbar.themed = 0 restores everything.
            var thumb = Solid(new Color(0.30f, 0.35f, 0.44f, 1f));
            var thumbHover = Solid(Accent);

            foreach (var style in new[] { GUI.skin.verticalScrollbarThumb,
                                          GUI.skin.horizontalScrollbarThumb })
            {
                style.normal.background = thumb;
                style.hover.background = thumbHover;
                style.active.background = thumbHover;
                style.focused.background = thumb;
                style.onNormal.background = thumb;
                style.onHover.background = thumbHover;
                style.onActive.background = thumbHover;
                style.onFocused.background = thumb;
            }
        }

        /// <summary>A one-pixel horizontal rule, for separating sections.</summary>        /// <summary>A one-pixel horizontal rule, for separating sections.</summary>
        internal void DrawDivider(float width)
        {
            var rect = GUILayoutUtility.GetRect(width, 1f);
            if (_line != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, rect.width, 1f), _line);
            }

            GUILayout.Space(11f);
        }

        internal int LineCount { get { return _line == null ? 0 : 1; } }

        internal void Release()
        {
            Destroy(ref _panel);
            Destroy(ref _button);
            Destroy(ref _buttonHover);
            Destroy(ref _field);
            Destroy(ref _header);
            Destroy(ref _line);
        }

        private static void Destroy(ref Texture2D texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }

        private static RectOffset Padding(int left, int right, int top, int bottom)
        {
            return new RectOffset(left, right, top, bottom);
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// A rounded rectangle, drawn small and nine-sliced by the style's border.
        /// </summary>
        /// <remarks>
        /// Anti-aliased by distance: a pixel is filled when it is inside the rounded
        /// shape, bordered when it is within a pixel of the edge, and blended in
        /// between. Doing it this way avoids the jagged corners that a plain
        /// rectangle-and-circle composite produces.
        /// </remarks>
        private static Texture2D Rounded(int radius, Color fill, Color border)
        {
            var r = Mathf.Clamp(radius, 0, 20);
            var size = r * 2 + 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            var centre = (size - 1) / 2f;
            var half = centre;

            // Half-extent of the straight part. The shape then fills the texture with
            // corners of exactly `r`. Getting this wrong (half - 1) left the shape a
            // full square, so nothing looked rounded at all.
            var inner = Mathf.Max(0f, half - r);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x - centre);
                    var dy = Mathf.Abs(y - centre);

                    // Distance outside a rounded rectangle of half-extent inner.
                    var ox = Mathf.Max(dx - inner, 0f);
                    var oy = Mathf.Max(dy - inner, 0f);
                    var distance = Mathf.Sqrt(ox * ox + oy * oy) - r;

                    var edge = Mathf.Clamp01(0.5f - distance);
                    if (edge <= 0.001f)
                    {
                        texture.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }

                    // The border occupies the outermost pixel of the shape.
                    // distance is negative inside the shape and 0 at its edge, so
                    // the border is where it is close to zero. Inverting this filled
                    // the entire texture with the border colour -- an opaque grey
                    // block with round corners.
                    var borderMix = Mathf.Clamp01(1f + distance);
                    var colour = Color.Lerp(fill, border, border.a > 0f ? borderMix : 0f);
                    colour.a = Mathf.Lerp(fill.a, border.a > 0f ? border.a : fill.a, borderMix) * edge;
                    texture.SetPixel(x, y, colour);
                }
            }

            texture.Apply();
            return texture;
        }
        /// <summary>
        /// A rounded checkbox: an empty box, or the same box with an inset square.
        /// </summary>
        /// <remarks>
        /// The checked state is a rounded square rather than a tick. The first version
        /// rasterised a tick and came out upside down, because Texture2D.SetPixel puts
        /// y=0 at the bottom while the tick was written as if y grew downwards. A
        /// centred, symmetric square cannot be flipped.
        /// </remarks>
        private static Texture2D CheckboxTexture(int size, bool on, Color fill, Color border,
                                                 Color mark)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            var radius = 4f;
            var centre = (size - 1) / 2f;
            var inner = centre - radius;
            var markInner = centre - 4f - 2f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x - centre);
                    var dy = Mathf.Abs(y - centre);

                    var coverage = Mathf.Clamp01(0.5f - RoundedDistance(dx, dy, inner, radius));
                    if (coverage <= 0.001f)
                    {
                        texture.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }

                    // The border sits on the outermost pixel of the box.
                    var borderMix = Mathf.Clamp01(1f + RoundedDistance(dx, dy, inner, radius));
                    var colour = Color.Lerp(fill, border, borderMix);
                    colour.a = Mathf.Lerp(fill.a, border.a, borderMix) * coverage;

                    if (on)
                    {
                        var markCoverage = Mathf.Clamp01(
                            0.5f - RoundedDistance(dx, dy, markInner, 2f));
                        if (markCoverage > 0f)
                        {
                            colour = new Color(mark.r, mark.g, mark.b,
                                               Mathf.Lerp(colour.a, mark.a, markCoverage));
                        }
                    }

                    texture.SetPixel(x, y, colour);
                }
            }

            texture.Apply();
            return texture;
        }
        /// <summary>Signed distance to a rounded rectangle, from a centred offset.</summary>
        private static float RoundedDistance(float dx, float dy, float inner, float radius)
        {
            var ox = Mathf.Max(dx - inner, 0f);
            var oy = Mathf.Max(dy - inner, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) - radius;
        }
    }

}
