using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>Business actions exposed to page views. No Unity objects here.</summary>
    internal sealed class ChillFocusPageActions
    {
        private readonly ConfigBridge _config;
        private readonly Action<bool> _setGate;
        private readonly BlockedAppsModel _blocked;
        private readonly ProcessListModel _processes;

        internal ChillFocusPageActions(ConfigBridge config, IOverlayActions actions,
                                       Action<bool> setGate, Action<string, bool> toast)
        {
            _config = config;
            _setGate = setGate;
            _blocked = new BlockedAppsModel(config);
            _processes = new ProcessListModel(actions, toast);
        }

        internal BlockedAppsModel Blocked { get { return _blocked; } }
        internal ProcessListModel Processes { get { return _processes; } }

        internal bool Mirror
        {
            get { return _config.MirrorExternalSession; }
            set { _config.MirrorExternalSession = value; }
        }

        internal bool LockTimerControls
        {
            get { return _config.LockTimerControls; }
            set { _config.LockTimerControls = value; }
        }

        internal string SyncText
        {
            get
            {
                return ChillFocusCopy.SyncValue(_config.MirrorTimeSyncSeconds);
            }
        }

        internal void SetFollowTimer(bool value)
        {
            if (_setGate != null) _setGate(value);
        }
    }
}
