using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>
    /// Creates native-looking rows from the cloned General page template.
    /// Contains no plugin state or network access.
    /// </summary>
    internal sealed class NativeRowFactory
    {
        private readonly GameObject _template;
        private readonly GameObject _content;

        internal NativeRowFactory(GameObject template, GameObject content)
        {
            _template = template;
            _content = content;
        }

        internal void Add(GameObject row)
        {
            if (row != null && _content != null) row.transform.SetParent(_content.transform, false);
        }

        internal GameObject Section(string title)
        {
            var row = CloneRow(title);
            DisableButtons(row);
            StretchLabel(row, 1f);
            return row;
        }

        internal GameObject Status(string title, string value)
        {
            var row = CloneRow(title);
            DisableButtons(row);
            StretchLabel(row, 1f);
            var label = Label(row);
            if (label != null) label.text = title + "    " + value;
            return row;
        }

        /// <summary>Row with a plain button: one-shot commands such as back or refresh.</summary>
        internal GameObject Action(string title, string value, Action action)
        {
            return ActionRow(title, value, action, false);
        }

        /// <summary>Row whose button keeps the native "using" highlight; pickers and navigation.</summary>
        internal GameObject PrimaryAction(string title, string value, Action action)
        {
            return ActionRow(title, value, action, true);
        }

        private GameObject ActionRow(string title, string value, Action action, bool usingHighlight)
        {
            var row = CloneRow(title);
            if (row == null) return null;
            StretchLabel(row, 0.70f);
            var on = FindButton(row, "OnButton");
            if (on != null) on.gameObject.SetActive(false);
            var off = FindButton(row, "OffButton");
            if (off == null) return row;
            off.gameObject.SetActive(true);
            NativeActionButton.Bind(row, off, value, action, usingHighlight);
            return row;
        }

        internal GameObject Divider()
        {
            var row = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(null, false);
            var image = row.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.22f);
            image.raycastTarget = false;
            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(NativePageLayout.DividerWidth, NativePageLayout.DividerHeight);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = NativePageLayout.DividerHeight;
            element.preferredHeight = NativePageLayout.DividerHeight;
            element.minWidth = NativePageLayout.DividerWidth;
            element.preferredWidth = NativePageLayout.DividerWidth;
            return row;
        }

        internal GameObject CloneRow(string title)
        {
            if (_template == null) return null;
            var row = UnityEngine.Object.Instantiate(_template);
            row.name = "ChillFocusRow";
            row.SetActive(true);
            ClearLocalizers(row);
            var label = Label(row);
            if (label != null) label.text = title;
            var rect = row.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(NativePageLayout.InfoRowWidth, NativePageLayout.RowHeight);
            }
            var element = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            element.minWidth = NativePageLayout.InfoRowWidth;
            element.preferredWidth = NativePageLayout.InfoRowWidth;
            element.minHeight = NativePageLayout.RowHeight;
            element.preferredHeight = NativePageLayout.RowHeight;
            return row;
        }





        internal static void StretchLabel(GameObject row, float widthRatio)
        {
            var label = Label(row);
            if (label == null) return;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(widthRatio, 1f);
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        internal static void SetRowWidth(GameObject row, float width)
        {
            var rect = row.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(width, NativePageLayout.RowHeight);
            var element = row.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }
        }

        internal void AddIcon(GameObject row, string name, Sprite sprite)
        {
            if (row == null || sprite == null) return;
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(row.transform, false);
            iconObject.transform.SetAsFirstSibling();
            var layout = iconObject.GetComponent<LayoutElement>();
            layout.minWidth = NativePageLayout.IconSize;
            layout.preferredWidth = NativePageLayout.IconSize;
            layout.minHeight = NativePageLayout.IconSize;
            layout.preferredHeight = NativePageLayout.IconSize;
            layout.ignoreLayout = true;
            var image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(NativePageLayout.IconSize, NativePageLayout.IconSize);
            rect.anchoredPosition = new Vector2(NativePageLayout.IconX, 0f);

            var label = Label(row);
            if (label != null && label.rectTransform != null)
                label.rectTransform.anchoredPosition += new Vector2(NativePageLayout.IconTextShift, 0f);
        }

        internal static TMP_Text Label(GameObject row)
        {
            return row == null ? null : row.GetComponentInChildren<TMP_Text>(true);
        }

        internal static Button FindButton(GameObject row, string name)
        {
            return row == null ? null : row.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name.Contains(name));
        }

        private static void DisableButtons(GameObject row)
        {
            if (row == null) return;
            foreach (var button in row.GetComponentsInChildren<Button>(true))
                button.gameObject.SetActive(false);
        }

        private static void ClearLocalizers(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                foreach (var component in text.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var name = component.GetType().FullName ?? string.Empty;
                    if (name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                        UnityEngine.Object.Destroy(component);
                }
            }
        }
    }
}
