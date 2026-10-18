using System;
using BepInEx;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Binds one application to a native row: icon, 70% name area, right action.
    /// The row factory only provides primitives.
    /// </summary>
    internal sealed class AppRowView
    {
        private readonly NativeRowFactory _rows;

        internal AppRowView(NativeRowFactory rows)
        {
            _rows = rows;
        }

        internal GameObject Selected(string name, Sprite icon, Action remove)
        {
            return Build(name, icon, name, ChillFocusCopy.Remove, remove);
        }

        internal GameObject Process(ProcessItem item, bool selected, Sprite icon, Action toggle)
        {
            var summary = ChillFocusCopy.AppSummary(item);
            return Build(item.name, icon, summary, selected ? ChillFocusCopy.Remove : ChillFocusCopy.Pause, toggle);
        }

        private GameObject Build(string name, Sprite icon, string labelText,
                                 string actionLabel, Action action)
        {
            var row = _rows.CloneRow(name);
            if (row == null) return null;
            NativeRowFactory.SetRowWidth(row, NativePageLayout.AppRowWidth);
            _rows.AddIcon(row, name, icon);

            var layout = row.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;

            var label = NativeRowFactory.Label(row);
            if (label != null)
            {
                label.text = labelText;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0.70f, 1f);
                rect.offsetMin = new Vector2(60f, 0f);
                rect.offsetMax = new Vector2(-8f, 0f);
            }

            var on = NativeRowFactory.FindButton(row, "OnButton");
            var off = NativeRowFactory.FindButton(row, "OffButton");
            if (on != null) on.gameObject.SetActive(false);
            if (off == null) return row;

            NativeActionButton.Bind(row, off, actionLabel, action, false);
            return row;
        }
    }
}
