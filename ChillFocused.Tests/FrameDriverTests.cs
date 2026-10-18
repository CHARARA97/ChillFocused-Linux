using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class FrameDriverTests
    {
        [Fact]
        public void Several_sources_in_one_frame_run_the_tick_once()
        {
            var sources = new List<string>();
            var driver = new FrameDriver(sources.Add);

            driver.Tick("Update", 10);
            driver.Tick("Canvas", 10);
            driver.Tick("onBeforeRender", 10);

            Assert.Single(sources);
            Assert.Equal("Update", sources[0]);
            Assert.Equal(10, driver.LastFrame);
        }

        [Fact]
        public void The_next_frame_runs_again()
        {
            var count = 0;
            var driver = new FrameDriver(_ => count++);

            driver.Tick("Update", 10);
            driver.Tick("Update", 11);

            Assert.Equal(2, count);
        }
    }
}
