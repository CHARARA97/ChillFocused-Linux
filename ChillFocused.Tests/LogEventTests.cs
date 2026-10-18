using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class LogEventTests
    {
        [Fact]
        public void Event_lines_are_greppable_and_do_not_contain_newlines()
        {
            var line = LogEvent.Line("sync.failed", "url=http://127.0.0.1:1", "reason=a\nb");

            Assert.StartsWith("event=plugin.sync.failed", line);
            Assert.Contains("url=http://127.0.0.1:1", line);
            Assert.DoesNotContain("\n", line);
        }

        [Fact]
        public void Prefix_is_added_once()
        {
            Assert.Equal("[ChillFocused] hello", LogEvent.Prefixed("hello"));
            Assert.Equal("[ChillFocused] hello", LogEvent.Prefixed("[ChillFocused] hello"));
        }
    }
}
