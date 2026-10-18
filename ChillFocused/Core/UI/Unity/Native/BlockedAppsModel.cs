using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>Read/write model for the persisted blocked-app list.</summary>
    internal sealed class BlockedAppsModel
    {
        private readonly ConfigBridge _config;

        internal BlockedAppsModel(ConfigBridge config)
        {
            _config = config;
        }

        internal List<string> Names()
        {
            return _config.Names();
        }

        internal bool Contains(string name)
        {
            return _config.HasName(name);
        }

        internal void Remove(string name)
        {
            _config.RemoveName(name);
        }

        internal void Toggle(string name)
        {
            if (_config.HasName(name)) _config.RemoveName(name);
            else _config.AddName(name);
        }
    }
}
