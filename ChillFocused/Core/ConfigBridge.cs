using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ChillFocused.Core
{
    /// <summary>
    /// Read/write access to the plugin's configuration, for the settings panel.
    /// </summary>
    /// <remarks>
    /// Assigning to a <c>ConfigEntry.Value</c> makes BepInEx write the .cfg file,
    /// so edits made in the panel are persisted and are also picked up by the
    /// headless sync thread on its next cycle. That means one edit path serves
    /// both the UI and the file.
    /// </remarks>
    internal sealed class ConfigBridge
    {
        private readonly ConfigEntry<HudMode> _hud;
        private readonly ConfigEntry<string> _processNames;
        private readonly ConfigEntry<string> _cmdlineSubstrings;
        private readonly ConfigEntry<string> _protectedNames;

        public ConfigBridge(
            ConfigEntry<HudMode> hud,
            ConfigEntry<string> processNames,
            ConfigEntry<string> cmdlineSubstrings, ConfigEntry<string> protectedNames)
        {
            _hud = hud;
            _processNames = processNames;
            _cmdlineSubstrings = cmdlineSubstrings;
            _protectedNames = protectedNames;
        }

        internal HudMode Hud
        {
            get { return _hud.Value; }
            set { _hud.Value = value; }
        }

        // -- blacklist: process names ------------------------------------------

        internal List<string> Names()
        {
            return RuleText.Split(_processNames.Value);
        }

        internal bool HasName(string name)
        {
            foreach (var existing in Names())
            {
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal void AddName(string name)
        {
            if (string.IsNullOrEmpty(name) || HasName(name))
            {
                return;
            }

            var names = Names();
            names.Add(name.Trim());
            SetNames(names);
        }

        internal void RemoveName(string name)
        {
            var names = Names();
            for (var i = names.Count - 1; i >= 0; i--)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    names.RemoveAt(i);
                }
            }

            SetNames(names);
        }

        internal void SetNames(IEnumerable<string> names)
        {
            _processNames.Value = string.Join("; ", new List<string>(names).ToArray());
        }

        // -- protect list: names that must never be blocked --------------------

        internal List<string> ProtectNames()
        {
            return RuleText.Split(_protectedNames.Value);
        }

        internal bool HasProtectName(string name)
        {
            foreach (var existing in ProtectNames())
            {
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal void AddProtectName(string name)
        {
            if (string.IsNullOrEmpty(name) || HasProtectName(name))
            {
                return;
            }

            var names = ProtectNames();
            names.Add(name.Trim());
            _protectedNames.Value = string.Join("; ", names.ToArray());
        }

        internal void RemoveProtectName(string name)
        {
            var names = ProtectNames();
            for (var i = names.Count - 1; i >= 0; i--)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    names.RemoveAt(i);
                }
            }

            _protectedNames.Value = string.Join("; ", names.ToArray());
        }

        // -- blacklist: command-line substrings --------------------------------

        internal List<string> Cmdlines()
        {
            return RuleText.Split(_cmdlineSubstrings.Value);
        }

        internal void AddCmdline(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                return;
            }

            var values = Cmdlines();
            foreach (var existing in values)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(value.Trim());
            _cmdlineSubstrings.Value = string.Join("; ", values.ToArray());
        }

        internal void RemoveCmdline(string value)
        {
            var values = Cmdlines();
            for (var i = values.Count - 1; i >= 0; i--)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    values.RemoveAt(i);
                }
            }

            _cmdlineSubstrings.Value = string.Join("; ", values.ToArray());
        }

    }
}
