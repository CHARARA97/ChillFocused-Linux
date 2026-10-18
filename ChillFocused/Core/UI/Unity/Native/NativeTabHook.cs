using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChillFocused.Core
{
    /// <summary>
    /// Hooks original tab buttons. On a native click it hides the injected page and
    /// explicitly restores the clicked native page/highlight; SettingService may
    /// still consider that tab selected and therefore not reactivate it by itself.
    /// </summary>
    internal sealed class NativeTabHook
    {
        private object _ui;
        private Action<GameObject, Component> _onNative;
        private readonly HashSet<Button> _hooked = new HashSet<Button>();

        internal void Attach(object ui, Action<GameObject, Component> onNative)
        {
            _ui = ui;
            _onNative = onNative;
            Hook();
        }

        internal void Hook()
        {
            if (_ui == null || _onNative == null) return;
            foreach (var pair in SettingUiReflection.Pages(_ui))
            {
                var page = pair.Item1;
                var tab = pair.Item2;
                var button = tab == null ? null : tab.GetComponent<Button>();
                if (button == null || !_hooked.Add(button)) continue;
                button.onClick.AddListener(() =>
                {
                    if (_onNative != null) _onNative(page, tab);
                });
            }
        }
    }
}
