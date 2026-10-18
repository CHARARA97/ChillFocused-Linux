using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>Pure construction helpers for cloning the game's native tab/page.</summary>
    internal static class NativePageCloner
    {
        internal static Component CloneTab(Component credits, Action onClick)
        {
            var tab = UnityEngine.Object.Instantiate(credits, credits.transform.parent);
            tab.gameObject.name = "ChillFocusTab";
            tab.transform.SetSiblingIndex(credits.transform.GetSiblingIndex() + 1);
            NativeInteractable.Setup(tab.gameObject);
            var button = tab.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
            ClearLocalizers(tab.gameObject);
            foreach (var text in tab.GetComponentsInChildren<TMP_Text>(true)) text.text = "ChillFocus";
            return tab;
        }

        internal static GameObject ClonePage(GameObject general, out ScrollRect scroll, out GameObject content)
        {
            var page = UnityEngine.Object.Instantiate(general);
            page.name = "ChillFocusPage";
            page.transform.SetParent(general.transform.parent, false);
            page.SetActive(false);
            RemoveChild(page.transform, "Title");

            scroll = page.GetComponentInChildren<ScrollRect>(true);
            if (scroll == null || scroll.content == null)
                throw new InvalidOperationException("no ScrollRect on cloned General page");

            content = scroll.content.gameObject;
            TrimToScrollRect(page.transform, scroll.transform);
            ClearChildren(content.transform);
            ConfigureContent(scroll);
            return page;
        }

        internal static void ConfigureContent(ScrollRect scroll)
        {
            var content = scroll.content;
            content.anchorMin = new Vector2(content.anchorMin.x, 1f);
            content.anchorMax = new Vector2(content.anchorMax.x, 0f);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>() ??
                         content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 8, 24);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.reverseArrangement = false;
            var fitter = content.GetComponent<ContentSizeFitter>() ??
                         content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void TrimToScrollRect(Transform pageRoot, Transform scroll)
        {
            Transform root = scroll;
            while (root.parent != null && root.parent != pageRoot) root = root.parent;
            if (root == pageRoot) return;
            foreach (Transform child in pageRoot) if (child != root) child.gameObject.SetActive(false);
        }

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }

        private static void RemoveChild(Transform root, string name)
        {
            var child = root.Find(name);
            if (child != null) UnityEngine.Object.Destroy(child.gameObject);
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
