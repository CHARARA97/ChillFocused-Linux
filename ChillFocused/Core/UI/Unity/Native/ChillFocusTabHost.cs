using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>
    /// Owns the cloned ChillFocus Tab/Page lifecycle. Construction details are in
    /// <see cref="NativePageCloner"/>; reflection is in <see cref="SettingUiReflection"/>.
    /// </summary>
    internal sealed class ChillFocusTabHost
    {
        private object _ui;
        private Component _tab;
        private GameObject _page;
        private GameObject _content;
        private GameObject _template;
        private ScrollRect _scroll;
        private Action _show;
        private Action _hide;
        private readonly NativeTabHook _tabHook = new NativeTabHook();

        internal GameObject Content { get { return _content; } }
        internal GameObject Template { get { return _template; } }
        internal bool Attached { get { return _page != null; } }

        internal bool TryAttach(Action show, Action hide)
        {
            if (Attached) return true;
            _show = show;
            _hide = hide;

            try
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                {
                    if (candidate == null || candidate.GetType().Name != "SettingUI") continue;
                    var general = SettingUiReflection.Field<GameObject>(candidate, "_generalParent");
                    var credits = SettingUiReflection.Field<Component>(candidate, "_creditsInteractableUI");
                    if (general == null || credits == null) continue;
                    Attach(candidate, general, credits);
                    return true;
                }
            }
            catch (Exception)
            {
                // Retry next tick; the game may still be instantiating SettingUI.
            }

            return false;
        }

        private void Attach(object ui, GameObject general, Component credits)
        {
            _ui = ui;
            _template = FindTemplate(general);
            _tab = NativePageCloner.CloneTab(credits,
                () => { if (_show != null) _show(); });
            _page = NativePageCloner.ClonePage(general, out _scroll, out _content);
            _tabHook.Attach(ui, OnNativeTab);
        }

        private void OnNativeTab(GameObject page, Component tab)
        {
            if (_page != null) _page.SetActive(false);
            NativeInteractable.SetUsing(_tab.gameObject, false);

            foreach (var pair in SettingUiReflection.Pages(_ui))
            {
                var isTarget = ReferenceEquals(pair.Item1, page);
                if (pair.Item1 != null) pair.Item1.SetActive(isTarget);
                if (pair.Item2 != null)
                {
                    if (isTarget) SettingUiReflection.Invoke(pair.Item2, "ActivateUseUI", false);
                    else SettingUiReflection.Invoke(pair.Item2, "DeactivateUseUI", false);
                }
            }
        }

        internal void Show()
        {
            if (!Attached) return;
            foreach (var pair in SettingUiReflection.Pages(_ui))
            {
                if (pair.Item1 != null) pair.Item1.SetActive(false);
                SettingUiReflection.Invoke(pair.Item2, "DeactivateUseUI", false);
            }

            _page.SetActive(true);
            NativeInteractable.SetUsing(_tab.gameObject, true);
        }

        internal void Hide()
        {
            if (_page != null) _page.SetActive(false);
            NativeInteractable.SetUsing(_tab.gameObject, false);
        }

        internal void Open(Action show)
        {
            if (!Attached) return;
            if (!SettingUiReflection.IsGeneralVisible(_ui)) SettingUiReflection.Activate(_ui);
            if (show != null) show();
            Show();
        }

        internal bool IsGeneralVisible()
        {
            return SettingUiReflection.IsGeneralVisible(_ui);
        }

        internal void ClearContent()
        {
            if (_content == null) return;
            for (var i = _content.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.transform.GetChild(i).gameObject);
        }

        internal void ResetScroll()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        internal void RebuildLayout()
        {
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content.GetComponent<RectTransform>());
        }

        private static GameObject FindTemplate(GameObject general)
        {
            var scroll = general == null ? null : general.GetComponentInChildren<ScrollRect>(true);
            if (scroll == null || scroll.content == null) return null;
            foreach (Transform child in scroll.content)
            {
                var buttons = child.GetComponentsInChildren<Button>(true);
                if (HasButton(buttons, "OnButton") && HasButton(buttons, "OffButton"))
                    return child.gameObject;
            }
            return null;
        }

        private static bool HasButton(Button[] buttons, string name)
        {
            foreach (var button in buttons) if (button.name.Contains(name)) return true;
            return false;
        }

        internal static TMP_Text Label(GameObject row)
        {
            return row == null ? null : row.GetComponentInChildren<TMP_Text>(true);
        }
    }
}
