using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Turns the three configurable hotkeys into edge-triggered actions.
    /// </summary>
    /// <remarks>
    /// Key binding entries are BepInEx types, but the edge/debounce rule is already
    /// in <see cref="Hotkeys"/>. This class is the small adapter that reads the
    /// entries and invokes callbacks; the runner no longer keeps three previous-key
    /// booleans and a key-down helper.
    /// </remarks>
    internal sealed class HotkeyController
    {
        private readonly ConfigEntry<KeyboardShortcut> _toggleHud;
        private readonly ConfigEntry<KeyboardShortcut> _toggleSettings;
        private readonly ConfigEntry<KeyboardShortcut> _reloadUi;
        private bool _prevHud;
        private bool _prevSettings;
        private bool _prevReload;

        internal HotkeyController(ConfigEntry<KeyboardShortcut> toggleHud,
                                  ConfigEntry<KeyboardShortcut> toggleSettings,
                                  ConfigEntry<KeyboardShortcut> reloadUi)
        {
            _toggleHud = toggleHud;
            _toggleSettings = toggleSettings;
            _reloadUi = reloadUi;
        }

        internal void Tick(Action toggleHud, Action toggleSettings, Action reloadUi)
        {
            if (Hotkeys.Edge(Down(_toggleHud), ref _prevHud) && toggleHud != null)
            {
                toggleHud();
            }

            if (Hotkeys.Edge(Down(_toggleSettings), ref _prevSettings) && toggleSettings != null)
            {
                toggleSettings();
            }

            if (Hotkeys.Edge(Down(_reloadUi), ref _prevReload) && reloadUi != null)
            {
                reloadUi();
            }
        }

        /// <summary>
        /// IMGUI fallback: some Proton/Wine paths swallow Input.GetKeyDown even
        /// though the key event reaches OnGUI. Handling KeyDown here also stops
        /// Tick() from double-firing because the edge state is already updated.
        /// </summary>
        internal void OnGui(Event current, Action toggleHud, Action toggleSettings, Action reloadUi)
        {
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            if (Matches(_toggleHud, current.keyCode) && toggleHud != null)
            {
                _prevHud = true;
                toggleHud();
            }

            if (Matches(_toggleSettings, current.keyCode) && toggleSettings != null)
            {
                _prevSettings = true;
                toggleSettings();
            }

            if (Matches(_reloadUi, current.keyCode) && reloadUi != null)
            {
                _prevReload = true;
                reloadUi();
            }
        }

        private static bool Matches(ConfigEntry<KeyboardShortcut> entry, KeyCode key)
        {
            return entry != null && entry.Value.MainKey != KeyCode.None
                   && entry.Value.MainKey == key;
        }

        private static bool Down(ConfigEntry<KeyboardShortcut> entry)
        {
            // KeyboardShortcut is a struct in BepInEx 5; an unbound entry never counts.
            return entry != null && entry.Value.IsDown();
        }
    }
}
