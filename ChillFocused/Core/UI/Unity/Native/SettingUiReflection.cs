using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The only reflection boundary around the game's SettingUI. Keeping this in
    /// one place makes game-version changes local instead of spreading field names
    /// through the page host.
    /// </summary>
    internal static class SettingUiReflection
    {
        internal static T Field<T>(object owner, string name) where T : class
        {
            if (owner == null) return null;
            var field = owner.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(owner) as T;
        }

        internal static object Invoke(object owner, string name, params object[] args)
        {
            if (owner == null) return null;
            var method = owner.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method == null ? null : method.Invoke(owner, args);
        }

        internal static bool IsGeneralVisible(object ui)
        {
            var general = Field<GameObject>(ui, "_generalParent");
            return general != null && general.activeInHierarchy;
        }

        internal static void Activate(object ui)
        {
            Invoke(ui, "Activate");
        }

        internal static IEnumerable<(GameObject, Component)> Pages(object ui)
        {
            var names = new[]
            {
                ("_generalParent", "_generalInteractableUI"),
                ("_graphicParent", "_graphicInteractableUI"),
                ("_audioParent", "_audioInteractableUI"),
                ("_accountParent", "_accountInteractableUI"),
                ("_newsParent", "_newsInteractableUI"),
                ("_creditsParent", "_creditsInteractableUI"),
            };
            foreach (var pair in names)
                yield return (Field<GameObject>(ui, pair.Item1), Field<Component>(ui, pair.Item2));
        }
    }
}
