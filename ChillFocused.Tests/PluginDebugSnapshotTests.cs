using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class PluginDebugSnapshotTests
    {
        [Fact]
        public void The_snapshot_line_carries_the_runtime_answers()
        {
            var snapshot = new PluginDebugSnapshot
            {
                Connected = true,
                Version = "0.2.0",
                SessionActive = true,
                GateSatisfied = true,
                FrozenCount = 3,
                FrozenTotal = 9,
                TimerKnown = true,
                TimerMode = TimerMode.Pomodoro,
                SyncConnected = true,
                BackendPid = 4242,
                SyncCycles = 12,
                SyncPushes = 2,
                PollEventCursor = 7,
                LastWarning = "boom\nnext",
            };

            var line = snapshot.ToLine();

            Assert.Contains("connected=true", line);
            Assert.Contains("version=0.2.0", line);
            Assert.Contains("session=true", line);
            Assert.Contains("frozen=3", line);
            Assert.Contains("frozen_total=9", line);
            Assert.Contains("backend_pid=4242", line);
            Assert.Contains("event_cursor=7", line);
            Assert.DoesNotContain("\n", line);
        }

        [Fact]
        public void An_idle_snapshot_is_still_one_line()
        {
            var line = new PluginDebugSnapshot().ToLine();

            Assert.Contains("connected=false", line);
            Assert.Contains("timer_mode=Unknown", line);
            Assert.DoesNotContain("\n", line);
        }
    }
}
