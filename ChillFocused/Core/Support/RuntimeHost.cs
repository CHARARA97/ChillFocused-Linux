using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Composes the overlay runner host and the sync host.
    /// </summary>
    internal sealed class RuntimeHost
    {
        private readonly RunnerHost _runners;
        private readonly SyncHost _sync;
        private readonly ExternalSessionMirror _mirror;
        private readonly Action<string> _logInfo;

        internal RuntimeHost(RuntimeHostContext context)
        {
            context = (context ?? new RuntimeHostContext()).Normalize();
            _logInfo = context.LogInfo;
            _runners = new RunnerHost(context);
            _sync = new SyncHost(context);
            _mirror = new ExternalSessionMirror(
                context.TimerControllerFactory(), context.ReportGate, context.SettingsProvider,
                StopExternalSession, context.LogInfo);
        }

        internal IFocusRunner Runner
        {
            get { return _runners.Runner; }
        }

        internal HeadlessRunner Sync
        {
            get { return _sync.Sync; }
        }

        internal void EnsureRunner(FocusSettings settings)
        {
            _runners.Ensure(settings);
        }

        internal void ObserveExternalSession(ExternalSessionState state)
        {
            _mirror.Observe(state);
        }

        internal void ObserveGameTimer(TimerState state)
        {
            _mirror.ObserveGameTimer(state);
        }

        private void StopExternalSession()
        {
            var runner = _runners.Runner;
            if (runner != null)
            {
                runner.StopExternalSession();
            }
        }

        internal void StartSync(FocusSettings settings)
        {
            _sync.Start(settings);
        }

        internal void SyncSettings()
        {
            _runners.SyncSettings();
        }

        internal void DestroyRunner()
        {
            _runners.Destroy();
        }

        internal void Wake()
        {
            _sync.Wake();
        }

        internal void Fire(string method, string path, string body)
        {
            _sync.Fire(method, path, body);
        }

        internal void DumpDebug()
        {
            var snapshot = _runners.Runner != null
                ? _runners.Runner.CaptureDebug()
                : new PluginDebugSnapshot();
            var sync = _sync.Sync;
            if (sync != null)
            {
                var debug = sync.CaptureDebug(GameClock.Now);
                snapshot.SyncConnected = debug.Connected;
                snapshot.RulesDue = debug.RulesDue;
                snapshot.BackendPid = debug.BackendPid;
                snapshot.SyncCycles = debug.Cycles;
                snapshot.SyncPushes = debug.Pushes;
                snapshot.LastHeartbeatAt = debug.LastHeartbeatAt;
                snapshot.LastSyncCycleMs = debug.LastCycleMs;
                snapshot.MaxSyncCycleMs = debug.MaxCycleMs;
                snapshot.SyncLastError = debug.LastError;
                snapshot.RuleLastError = debug.RuleLastError;
            }

            _logInfo("debug " + snapshot.ToLine());
        }
    }
}
