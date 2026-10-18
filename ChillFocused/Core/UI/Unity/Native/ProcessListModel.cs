using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Process picker state and Focused API access. Deliberately Unity-free apart
    /// from ProcessItem/ProcessIconCache data shapes.
    /// </summary>
    internal sealed class ProcessListModel
    {
        private readonly IOverlayActions _actions;
        private readonly Action<string, bool> _toast;
        private ProcessItem[] _items = new ProcessItem[0];

        internal ProcessListModel(IOverlayActions actions, Action<string, bool> toast)
        {
            _actions = actions;
            _toast = toast;
        }

        internal ProcessItem[] Items { get { return _items; } }
        internal bool Fetching { get; private set; }

        internal void Load(Action changed)
        {
            if (_actions == null || Fetching || _actions.Busy) return;
            Fetching = true;
            _actions.GetProcessesAsync(400, (response, error) =>
            {
                Fetching = false;
                if (error != null || response == null || !response.ok)
                {
                    if (_toast != null) _toast("无法读取进程列表", true);
                    return;
                }

                _items = response.ToItems();
                if (changed != null) changed();
            });
        }
    }
}
