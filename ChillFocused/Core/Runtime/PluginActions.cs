using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// The small set of imperative actions the settings panel and wiring can invoke.
    /// </summary>
    /// <remarks>
    /// These used to be private methods on the BepInEx plugin, mixed with lifecycle,
    /// frame hooks and config watching. Keeping them here makes the entry point a
    /// composition root rather than an action registry.
    /// </remarks>
    internal sealed class PluginActions
    {
        private readonly PluginConfig _options;
        private readonly RuntimeHost _host;
        private readonly ConfigWatcher _watcher;
        private readonly Func<FocusSettings> _readSettings;
        private readonly Action<string> _logInfo;

        internal PluginActions(PluginConfig options, RuntimeHost host, ConfigWatcher watcher,
                               Func<FocusSettings> readSettings, Action<string> logInfo)
        {
            _options = options;
            _host = host;
            _watcher = watcher;
            _readSettings = readSettings ?? (() => new FocusSettings());
            _logInfo = logInfo ?? (message => { });
        }

        internal void SetGameGate(bool followTimer)
        {
            _options.AlwaysOn.Value = !followTimer;
            if (!followTimer)
            {
                _options.Enabled.Value = true;
            }

            ApplySettingsNow();
            _logInfo("game gate set to " +
                     (followTimer ? "follow the game timer" : "always on"));
        }

        internal void SetDryRun(bool dryRun)
        {
            _options.DryRun.Value = dryRun;
            ApplySettingsNow();
            _logInfo("dry-run set to " + dryRun);
        }

        internal void ThawNow()
        {
            _host.Fire("POST", "/api/v1/thaw", "{}");
            _logInfo("asked Focused to resume every suspended target");
        }

        internal void ScanNow()
        {
            _host.Fire("POST", "/api/v1/scan", "{}");
        }

        internal void ApplySettingsNow()
        {
            var runner = _host.Runner;
            if (runner == null)
            {
                return;
            }

            if (_watcher != null)
            {
                _watcher.Invalidate();
            }

            runner.ApplySettings(_readSettings());
        }

        internal void Toast(string message, bool warning)
        {
            var runner = _host.Runner;
            if (runner != null)
            {
                runner.ShowToast(message, warning);
            }
            else
            {
                _logInfo("[panel] " + message);
            }
        }

        internal void PersistHudMode(HudMode value)
        {
            _options.HudMode.Value = value;
            if (_watcher != null)
            {
                _watcher.Invalidate();
            }
        }
    }
}
