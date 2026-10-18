using System;
using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class RuntimeHostTests
    {
        private sealed class FakeRunner : IFocusRunner
        {
            public bool Alive = true;
            public int Destroys;
            public int Ticks;
            public int ClientSets;
            public int Applies;
            public FocusSettings CurrentSettings { get; set; } = new FocusSettings();
            public bool IsAlive { get { return Alive; } }
            public void SetClient(IFocusedEndpoint client) { ClientSets++; }
            public void ApplySettings(FocusSettings settings) { Applies++; CurrentSettings = settings; }
            public void Tick() { Ticks++; }
            public void ShowToast(string message, bool warning) { }
            public void StopExternalSession() { }
            public PluginDebugSnapshot CaptureDebug() { return new PluginDebugSnapshot { Version = "fake" }; }
            public void DestroyRunner() { Alive = false; Destroys++; }
        }

        [Fact]
        public void EnsureRunner_is_idempotent_while_alive_and_recreates_after_destroy()
        {
            var created = 0;
            var runner = new FakeRunner();
            var host = NewHost(() => runner, () => created++);

            host.EnsureRunner(new FocusSettings());
            host.EnsureRunner(new FocusSettings());
            Assert.Equal(1, created);

            runner.Alive = false;
            host.EnsureRunner(new FocusSettings());
            Assert.Equal(2, created);

            host.DestroyRunner();
            Assert.Equal(1, runner.Destroys);
        }

        [Fact]
        public void SyncSettings_replaces_the_client_only_when_the_endpoint_changes()
        {
            var runner = new FakeRunner();
            var settings = new FocusSettings { FocusedUrl = "http://127.0.0.1:8766" };
            runner.CurrentSettings = settings;
            var host = NewHost(() => runner, () => { }, () => settings);
            host.EnsureRunner(settings);

            host.SyncSettings();
            Assert.Equal(0, runner.ClientSets);
            Assert.Equal(1, runner.Applies);

            var changed = new FocusSettings { FocusedUrl = "http://127.0.0.1:9999" };
            settings = changed;
            host.SyncSettings();
            Assert.Equal(1, runner.ClientSets);
            Assert.Equal(2, runner.Applies);
        }

        [Fact]
        public void A_factory_failure_is_logged_and_retried()
        {
            var calls = 0;
            var errors = new List<string>();
            var host = new RuntimeHost(new RuntimeHostContext
            {
                SettingsProvider = () => new FocusSettings(),
                RunnerFactory = _ =>
                {
                    calls++;
                    throw new InvalidOperationException("no scene");
                },
                ClientFactory = _ => null,
                OnCycle = () => { },
                LogInfo = _ => { },
                LogWarning = _ => { },
                LogError = errors.Add,
            });

            host.EnsureRunner(new FocusSettings());
            host.EnsureRunner(new FocusSettings());

            Assert.Equal(2, calls);
            Assert.Equal(2, errors.Count);
        }

        private static RuntimeHost NewHost(Func<IFocusRunner> runner,
                                           Action onCreated,
                                           Func<FocusSettings> settings = null)
        {
            return new RuntimeHost(new RuntimeHostContext
            {
                SettingsProvider = settings ?? (() => new FocusSettings()),
                RunnerFactory = _ =>
                {
                    onCreated();
                    return runner();
                },
                ClientFactory = _ => null,
                OnCycle = () => { },
                LogInfo = _ => { },
                LogWarning = _ => { },
                LogError = _ => { },
            });
        }
    }
}
