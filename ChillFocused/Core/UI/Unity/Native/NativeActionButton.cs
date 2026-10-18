using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>
    /// Single owner of an app/action button: position, size, text, click binding and
    /// the native "using" look. It never repaints the button: the cloned Image keeps
    /// its own sprite, colour and draw type, so the game's rounded corners and
    /// gradient survive. Replacing them with a flat texture turns the button into a
    /// solid rectangle, because a plain white sprite has no corner or gradient data.
    /// </summary>
    internal static class NativeActionButton
    {
        /// <param name="usingHighlight">
        /// The game's "using" state, drawn as the bright gradient border. Reserve it
        /// for buttons that pick or navigate; one-shot commands (add/remove/back/refresh)
        /// stay plain so the highlight keeps meaning "this one is the way forward".
        /// Always set explicitly: the cloned Off button may already carry the state.
        /// </param>
        internal static void Bind(GameObject row, Button button, string label, Action action,
                                  bool usingHighlight)
        {
            if (row == null || button == null) return;

            var rowRect = row.GetComponent<RectTransform>();
            var target = button.transform.parent as RectTransform;
            if (target == null || target == rowRect) target = button.GetComponent<RectTransform>();
            if (target == null) return;

            target.anchorMin = new Vector2(1f, 0.5f);
            target.anchorMax = new Vector2(1f, 0.5f);
            target.pivot = new Vector2(1f, 0.5f);
            target.anchoredPosition = new Vector2(-18f, 0f);
            target.sizeDelta = new Vector2(NativePageLayout.ActionWidth, NativePageLayout.ActionHeight);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.sizeDelta = target.sizeDelta;

            button.gameObject.SetActive(true);
            button.interactable = true;
            NativeInteractable.SetUsing(button.gameObject, usingHighlight);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.alignment = TextAlignmentOptions.Center;
                text.text = label;
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4f, 0f);
                rect.offsetMax = new Vector2(-4f, 0f);
            }
        }
    }
}
