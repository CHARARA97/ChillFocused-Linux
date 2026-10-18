using System;

namespace ChillFocused.Core
{
    /// <summary>Owns the overlay runner's lifecycle and settings updates.</summary>
    internal sealed class RunnerHost
    {
        private readonly Func<FocusSettings, IFocusRunner> _factory;
        private readonly Func<FocusSettings, IFocusedEndpoint> _clientFactory;
        private readonly Func<FocusSettings> _settingsProvider;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;

        internal RunnerHost(RuntimeHostContext context)
        {
            _factory = context.RunnerFactory;
            _clientFactory = context.ClientFactory;
            _settingsProvider = context.SettingsProvider;
            _logWarning = context.LogWarning;
            _logError = context.LogError;
        }

        internal IFocusRunner Runner { get; private set; }

        internal void Ensure(FocusSettings settings)
        {
            if (Runner != null && Runner.IsAlive)
            {
                return;
            }

            Runner = null;
            _logWarning("FocusRunner is missing (destroyed or never created); recreating it");

            try
            {
                Runner = _factory(settings ?? new FocusSettings());
            }
            catch (Exception ex)
            {
                _logError("could not initialise the overlay: " +
                          ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        internal void SyncSettings()
        {
            if (Runner == null)
            {
                return;
            }

            var settings = _settingsProvider();
            var current = Runner.CurrentSettings;
            if (current == null || !settings.EndpointEquals(current))
            {
                Runner.SetClient(_clientFactory(settings));
            }

            Runner.ApplySettings(settings);
        }

        internal void Destroy()
        {
            var runner = Runner;
            Runner = null;
            if (runner != null)
            {
                try
                {
                    runner.DestroyRunner();
                }
                catch (Exception ex)
                {
                    _logWarning("could not destroy the overlay: " + ex.Message);
                }
            }
        }
    }
}
