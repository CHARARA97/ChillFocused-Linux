using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>Resolves process names to Sprites. Kept out of the data model.</summary>
    internal sealed class ProcessIconView
    {
        private readonly ProcessListModel _model;
        private readonly ProcessIconCache _cache = new ProcessIconCache();

        internal ProcessIconView(ProcessListModel model)
        {
            _model = model;
        }

        internal Sprite Icon(string name)
        {
            var items = _model.Items;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item != null && string.Equals(item.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return _cache.Get(item.name, item.icon, out _);
            }
            return _cache.Get(name, string.Empty, out _);
        }
    }
}
