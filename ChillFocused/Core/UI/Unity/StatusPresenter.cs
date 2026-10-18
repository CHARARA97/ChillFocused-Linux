using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Status side effects for the headless/UI-less plugin path. The injected
    /// ChillFocus page reads FocusStatus directly; this class only logs transitions
    /// and keeps the coordinator independent from any renderer.
    /// </summary>
    internal sealed class StatusPresenter
    {
        private readonly Action<string, bool> _toast;
        private readonly Action<string> _inform;
        private bool _loggedGate;
        private bool _loggedVersionDiagnostics;
        private bool _previousEnforcing;

        internal StatusPresenter(Action<string, bool> toast, Action<string> inform)
        {
            _toast = toast ?? ((message, warning) => { });
            _inform = inform ?? (message => { });
        }

        internal void Connected(FocusStatus previous, FocusStatus current,
                                FocusSettings settings, string baseUrl, bool hasVersion)
        {
            if (!previous.Connected)
                _inform(LogEvent.Line("status.connected", "session=" + (current.SessionActive ? "on" : "off")));

            if (!hasVersion && !_loggedVersionDiagnostics)
            {
                _loggedVersionDiagnostics = true;
                _inform("status parse: no version in the response");
            }

            var gateEnabled = !settings.AlwaysOn;
            var gateSatisfied = current.SessionActive;
            if (!_loggedGate || previous.GateEnabled != gateEnabled || previous.GateSatisfied != gateSatisfied)
            {
                _loggedGate = true;
                _inform("gate now: follow-timer=" + gateEnabled +
                        " timer=" + (current.Timer.Working ? "working" : "idle") +
                        " session=" + current.SessionActive);
            }

            var enforcing = current.Connected && current.SessionActive;
            if (enforcing != _previousEnforcing)
            {
                _previousEnforcing = enforcing;
                _toast(enforcing ? "开始创作 · 已冻结屏蔽名单应用" : "创作结束 · 已全部恢复", false);
            }
        }

        internal void Warning(string message)
        {
            _inform("status warning: " + (message ?? string.Empty));
        }

        internal void Disconnected(FocusStatus current, string baseUrl)
        {
            _previousEnforcing = false;
            _inform("status disconnected: " + (baseUrl ?? string.Empty));
        }
    }
}
