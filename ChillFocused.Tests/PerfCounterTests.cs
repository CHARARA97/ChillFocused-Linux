using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class PerfCounterTests
    {
        [Fact]
        public void Records_count_last_max_and_average()
        {
            var counter = new PerfCounter();

            counter.Record(10);
            counter.Record(30);
            counter.Record(20);

            Assert.Equal(3, counter.Count);
            Assert.Equal(20, counter.LastMs);
            Assert.Equal(30, counter.MaxMs);
            Assert.Equal(20, counter.AverageMs);
        }

        [Fact]
        public void Negative_samples_are_clamped()
        {
            var counter = new PerfCounter();
            counter.Record(-5);
            Assert.Equal(0, counter.LastMs);
            Assert.Equal(0, counter.MaxMs);
        }
    }
}
