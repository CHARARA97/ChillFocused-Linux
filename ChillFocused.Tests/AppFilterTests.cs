using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class AppFilterTests
    {
        [Fact]
        public void An_empty_filter_matches_every_row()
        {
            Assert.True(AppFilter.Matches(string.Empty, "firefox"));
            Assert.True(AppFilter.Matches(null, "firefox"));
        }

        [Fact]
        public void The_filter_matches_anywhere_in_the_name()
        {
            Assert.True(AppFilter.Matches("fox", "firefox"));
            Assert.True(AppFilter.Matches("firefox", "firefox"));
        }

        [Fact]
        public void The_filter_ignores_case()
        {
            Assert.True(AppFilter.Matches("FIREFOX", "firefox"));
            Assert.True(AppFilter.Matches("wineserver", "WineServer"));
        }

        [Fact]
        public void The_filter_can_miss()
        {
            Assert.False(AppFilter.Matches("chrome", "firefox"));
            Assert.False(AppFilter.Matches("firefox2", "firefox"));
        }

        [Fact]
        public void A_missing_name_never_matches_a_filter()
        {
            Assert.False(AppFilter.Matches("firefox", null));
        }
    }
}
