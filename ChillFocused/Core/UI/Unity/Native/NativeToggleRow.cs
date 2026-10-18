using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>Native On/Off toggle row. Owns only the row state binding.</summary>
    internal sealed class NativeToggleRow
    {
        private readonly NativeRowFactory _rows;

        internal NativeToggleRow(NativeRowFactory rows)
        {
            _rows = rows;
        }

        internal GameObject Build(string title, bool value, Action<bool> changed)
        {
            var row = _rows.CloneRow(title);
            if (row == null) return null;
            NativeRowFactory.StretchLabel(row, 0.60f);
            var on = NativeRowFactory.FindButton(row, "OnButton");
            var off = NativeRowFactory.FindButton(row, "OffButton");
            if (on == null || off == null) return row;

            Action<bool> apply = state =>
            {
                on.interactable = !state;
                off.interactable = state;
                NativeInteractable.SetUsing(on.gameObject, state);
                NativeInteractable.SetUsing(off.gameObject, !state);
                changed(state);
            };
            on.onClick.RemoveAllListeners();
            off.onClick.RemoveAllListeners();
            on.onClick.AddListener(() => apply(true));
            off.onClick.AddListener(() => apply(false));
            apply(value);
            return row;
        }
    }
}
