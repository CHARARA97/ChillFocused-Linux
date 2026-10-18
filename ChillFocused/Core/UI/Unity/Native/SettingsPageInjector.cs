using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Page controller for the injected ChillFocus tab. It only attaches the native
    /// host, selects Main/ProcessPage and asks the view to build itself.
    /// </summary>
    internal sealed class SettingsPageInjector
    {
        private readonly Func<FocusStatus> _status;
        private readonly Action<string> _log;
        private readonly ChillFocusTabHost _session = new ChillFocusTabHost();
        private readonly ChillFocusPageActions _actions;
        private readonly ProcessIconView _icons;
        private readonly MainPageView _main = new MainPageView();
        private readonly ProcessPageView _processes = new ProcessPageView();
        private NativeRowFactory _rows;
        private AppRowView _appRows;
        private bool _pickerMode;
        private bool _built;

        internal SettingsPageInjector(ConfigBridge config, IOverlayActions actions,
                                      Func<FocusStatus> status,
                                      Action<bool> setGate,
                                      Action<string, bool> toast, Action<string> log)
        {
            _status = status;
            _log = log ?? (message => { });
            _actions = new ChillFocusPageActions(config, actions, setGate, toast);
            _icons = new ProcessIconView(_actions.Processes);
        }

        internal void Tick()
        {
            if (_built) return;
            if (!_session.TryAttach(Show, Hide)) return;

            if (_session.Template == null)
                _log("settings-inject: no native row template; the ChillFocus page stays blank");

            _rows = new NativeRowFactory(_session.Template, _session.Content);
            _appRows = new AppRowView(_rows);
            BuildRows();
            _built = true;
            _log("settings-inject: added ChillFocus tab");
        }

        private void BuildRows()
        {
            if (_rows == null) return;
            _session.ClearContent();
            if (_pickerMode)
                _processes.Build(_rows, _appRows, _icons, _actions, ClosePicker, RebuildRows);
            else
                _main.Build(_rows, _appRows, _icons, _actions, _status(), OpenPicker, RebuildRows);
            _session.RebuildLayout();
        }

        private void OpenPicker()
        {
            _pickerMode = true;
            _actions.Processes.Load(RebuildRows);
            RebuildRows();
            _session.ResetScroll();
        }

        private void ClosePicker()
        {
            _pickerMode = false;
            RebuildRows();
            _session.ResetScroll();
        }

        private void RebuildRows()
        {
            BuildRows();
        }

        internal void ToggleSettings()
        {
            if (_session.Attached && !_session.IsGeneralVisible())
                _session.Open(Show);
        }

        internal void OpenChillFocus()
        {
            _session.Open(Show);
        }

        private void Show()
        {
            _session.Show();
            BuildRows();
        }

        private void Hide()
        {
            _session.Hide();
        }
    }
}
