using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// The dependencies RuntimeHost needs, grouped so construction stays readable.
    /// </summary>
    /// <remarks>
    /// The host used to take eight delegates in its constructor call, which made it
    /// hard to see which argument supplied logging, settings or the runner factory.
    /// This context is the single assembly point for the runtime composition.
    /// </remarks>
    internal sealed class RuntimeHostContext
    {
        internal Func<FocusSettings> SettingsProvider;
        internal Func<FocusSettings, IFocusRunner> RunnerFactory;
        internal Func<FocusSettings, IFocusedEndpoint> ClientFactory;
        internal ITimerSource TimerSource;
        internal ISessionReportGate ReportGate;
        internal Func<ITimerController> TimerControllerFactory;
        internal Action OnCycle;
        internal Action<string> LogInfo;
        internal Action<string> LogWarning;
        internal Action<string> LogError;

        internal RuntimeHostContext Normalize()
        {
            SettingsProvider = SettingsProvider ?? (() => new FocusSettings());
            RunnerFactory = RunnerFactory ?? (_ => null);
            ClientFactory = ClientFactory ?? (_ => null);
            TimerSource = TimerSource ?? new NullTimerSource();
            ReportGate = ReportGate ?? new SessionReportGate();
            TimerControllerFactory = TimerControllerFactory ?? (() => new NullTimerController());
            OnCycle = OnCycle ?? (() => { });
            LogInfo = LogInfo ?? (message => { });
            LogWarning = LogWarning ?? (message => { });
            LogError = LogError ?? (message => { });
            return this;
        }
    }
}
