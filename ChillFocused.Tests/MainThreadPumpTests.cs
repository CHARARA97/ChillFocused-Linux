using System;
using System.Threading;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class MainThreadPumpTests
    {
        private sealed class ImmediateContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback callback, object state)
            {
                callback(state);
            }
        }

        [Fact]
        public void Posted_work_runs_through_the_captured_context()
        {
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new ImmediateContext());
                var pump = new MainThreadPump(_ => { }, _ => { });
                Assert.True(pump.Install());

                var ran = 0;
                pump.Post(() => ran++);

                Assert.Equal(1, ran);
                Assert.Equal(1, pump.Posts);
                Assert.Equal(1, pump.Runs);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }
    }
}
