using System;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Thin bridge to the game's InteractableUI. Do not mutate its images here:
    /// the component owns Base/MouseOver/Using tweens.
    /// </summary>
    internal static class NativeInteractable
    {
        internal static void Setup(GameObject root)
        {
            var component = Find(root);
            if (component != null) SettingUiReflection.Invoke(component, "Setup");
        }

        internal static void SetUsing(GameObject root, bool usingState)
        {
            var component = Find(root);
            if (component != null)
                SettingUiReflection.Invoke(component, usingState ? "ActivateUseUI" : "DeactivateUseUI", false);
        }

        private static Component Find(GameObject root)
        {
            if (root == null) return null;
            var direct = root.GetComponent("InteractableUI") as Component;
            if (direct != null) return direct;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "InteractableUI")
                    return component;
            }
            return null;
        }
    }
}
