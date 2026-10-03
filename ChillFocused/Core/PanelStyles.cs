using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The settings window's look: the theme, the styles drawn with it, and the checkbox
    /// that cannot be restyled any other way.
    /// </summary>
    /// <remarks>
    /// Split out of the window, which keeps only state and layout. Everything here is
    /// rebuilt from the layout file, so a hot-reload costs one rebuild rather than one
    /// per frame.
    /// </remarks>
    internal sealed class PanelStyles
    {
        private readonly UiTheme _theme = new UiTheme();
        private Texture2D _background;
        private GUIStyle _section;
        private GUIStyle _hint;
        private GUIStyle _window;
        private GUIStyle _headline;
        private GUIStyle _row;
        private GUIStyle _header;

        /// <summary>The list styles the picker borrows; refilled on every rebuild.</summary>
        internal readonly PickerStyles Picker = new PickerStyles();

        internal GUIStyle Window { get { return _window; } }

        internal GUIStyle Section { get { return _section; } }

        internal GUIStyle Hint { get { return _hint; } }

        internal GUIStyle Headline { get { return _headline; } }

        internal GUIStyle Row { get { return _row; } }

        internal GUIStyle Header { get { return _header; } }

        /// <summary>
        /// Take the backdrop the window is drawn over. A different texture means a
        /// different source of rounded panels, so the styles are rebuilt against it.
        /// </summary>
        internal void SetBackground(Texture2D background)
        {
            if (ReferenceEquals(background, _background))
            {
                return;
            }

            _background = background;
            Rebuild();
        }

        /// <summary>Drop every style so the next <see cref="Ensure"/> rebuilds them.</summary>
        internal void Rebuild()
        {
            _window = null;
            _section = null;
            _hint = null;
            _headline = null;
            _row = null;
            _header = null;
        }

        /// <summary>Build the styles if they are missing, against the current spec.</summary>
        internal void Ensure(PanelSpec spec)
        {
            if (_section != null)
            {
                return;
            }

            // The same font the overlay borrowed, so Chinese renders here too.
            var font = HudFont.Resolve(spec.FontFamily);

            _theme.Refresh(spec, font);

            _window = _theme.Window;
            _headline = _theme.Headline;
            _section = _theme.Section;
            _hint = _theme.HintStyle;
            _row = _theme.Row;
            _header = _theme.Header;

            // The picker draws from the same theme, through its own bundle.
            Picker.Row = _theme.Row;
            Picker.Hint = _theme.HintStyle;
            Picker.Header = _theme.Header;
            Picker.Blocked = _theme.RowBlocked;
        }

        /// <summary>A themed horizontal rule; the theme owns the texture.</summary>
        internal void DrawDivider(float width)
        {
            _theme.DrawDivider(width);
        }

        /// <summary>
        /// A checkbox drawn from the theme instead of the built-in skin.
        /// </summary>
        /// <remarks>
        /// Unity's toggle style paints its box from the skin's own images, which are
        /// flat light-grey squares; there is no supported way to restyle them, so the
        /// control is drawn here. The whole row is the hit target, which also makes it
        /// easier to click than a bare 18px box.
        /// </remarks>
        internal bool Checkbox(string label, bool value, PanelSpec spec)
        {
            var height = Mathf.Max(26f, spec.BodyFontSize + 12f);
            var rect = GUILayoutUtility.GetRect(10f, 10000f, height, height,
                                                GUILayout.ExpandWidth(true));

            var boxSize = 18f;
            var box = new Rect(rect.x, rect.y + (height - boxSize) / 2f, boxSize, boxSize);

            if (Event.current.type == EventType.Repaint && _theme.CheckboxOff != null)
            {
                GUI.DrawTexture(box, value ? _theme.CheckboxOn : _theme.CheckboxOff);
            }

            var labelRect = new Rect(box.xMax + 10f, rect.y,
                                     Mathf.Max(0f, rect.width - boxSize - 16f), height);
            GUI.Label(labelRect, label, _theme.Row);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return !value;
            }

            return value;
        }
    }
}
