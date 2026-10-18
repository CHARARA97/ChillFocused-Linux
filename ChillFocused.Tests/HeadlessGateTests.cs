using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class HeadlessGateTests
    {
        [Fact]
        public void External_ownership_withdraws_once_then_stays_quiet()
        {
            var bodies = new List<string>();
            var gate = new SessionReportGate();
            var runner = new HeadlessRunner(
                () => new FocusSettings { SessionLeaseSeconds = 30 },
                new HttpTransport("http://127.0.0.1:1", string.Empty, 100),
                new FocusStatusProbe(() => "{\"ok\":true,\"active\":true,\"pid\":1}"),
                new RulePublisher(_ => { }, _ => { }, _ => { }),
                new SessionReporter(bodies.Add),
                0.5,
                _ => { },
                _ => { },
                gate);
            runner.GameStateProvider = () => new TimerState
            {
                Known = true,
                Mode = TimerMode.Pomodoro,
                Working = true,
                Running = true,
                Phase = "Work",
                ReadAt = GameClock.Now,
            };

            gate.EnterExternal();
            runner.Cycle();
            runner.Cycle();
            Assert.Single(bodies);
            Assert.Contains("\"active\":false", bodies[0]);

            gate.LeaveExternal();
            runner.Cycle();
            Assert.Equal(2, bodies.Count);
            Assert.Contains("\"active\":true", bodies[1]);
        }
    }
}
