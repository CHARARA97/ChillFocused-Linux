using System.Threading;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class HeadlessLifecycleTests
    {
        [Fact]
        public void Start_runs_cycles_and_stop_ends_the_thread()
        {
            var cycles = 0;
            var settings = new FocusSettings { SessionLeaseSeconds = 30 };
            var probe = new FocusStatusProbe(() =>
            {
                Interlocked.Increment(ref cycles);
                return "{\"ok\":true,\"active\":true,\"pid\":1}";
            });
            var runner = new HeadlessRunner(
                () => settings,
                new HttpTransport("http://127.0.0.1:1", string.Empty, 100),
                probe,
                new RulePublisher(_ => { }, _ => { }, _ => { }),
                new SessionReporter(_ => { }),
                0.5,
                _ => { },
                _ => { });

            runner.Start();
            var deadline = System.Environment.TickCount + 3000;
            while (runner.CycleCount < 2 && System.Environment.TickCount < deadline)
            {
                Thread.Sleep(50);
            }

            runner.Stop();

            Assert.True(runner.CycleCount >= 2);
            Assert.True(cycles >= 2);
        }
    }
}
