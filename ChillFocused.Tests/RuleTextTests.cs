using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The plugin's rule parsing must agree with Focused's. Everything the
    /// Focused does to a rule (lower-casing, globbing, 15-character truncation)
    /// has a mirror image here, and these tests pin that mirror down.
    /// </summary>
    public class RuleTextTests
    {
        [Fact]
        public void Split_handles_the_documented_separators()
        {
            var result = RuleText.Split("firefox; discord\nslack\r\nchromium");

            Assert.Equal(new[] { "firefox", "discord", "slack", "chromium" }, result);
        }

        [Fact]
        public void Split_trims_and_drops_empty_entries()
        {
            var result = RuleText.Split("  ; firefox ;;   ;discord;  ");

            Assert.Equal(new[] { "firefox", "discord" }, result);
        }

        [Fact]
        public void Split_removes_duplicates_but_keeps_order()
        {
            var result = RuleText.Split("b; a; b; a; c");

            Assert.Equal(new[] { "b", "a", "c" }, result);
        }

        [Fact]
        public void Split_does_not_treat_comma_as_a_separator()
        {
            // Command lines legitimately contain commas; splitting on them would
            // silently mangle a substring rule like "--flag=a,b".
            var result = RuleText.Split("--flag=a,b; other");

            Assert.Equal(new[] { "--flag=a,b", "other" }, result);
        }

        [Fact]
        public void Split_of_null_or_empty_is_empty()
        {
            Assert.Empty(RuleText.Split(null));
            Assert.Empty(RuleText.Split(string.Empty));
            Assert.Empty(RuleText.Split("   ;  ; "));
        }

        [Theory]
        [InlineData("*", true)]
        [InlineData("**", true)]
        [InlineData("?", true)]
        [InlineData("*?", true)]
        [InlineData("firefox", false)]
        [InlineData("chrom*", false)]
        [InlineData("", false)]
        public void IsCatchAllPattern_matches_the_Focused(string pattern, bool expected)
        {
            Assert.Equal(expected, RuleText.IsCatchAllPattern(pattern));
        }

        [Theory]
        [InlineData("firefox", "firefox", true)]
        [InlineData("firefox", "firefo", false)]
        [InlineData("chrom*", "chromium", true)]
        [InlineData("chrom*", "chrome", true)]
        [InlineData("chrom*", "not-chrome", false)]
        [InlineData("a*b*c", "axxbxxc", true)]
        [InlineData("a*b*c", "abc", true)]
        [InlineData("a*b*c", "ac", false)]
        [InlineData("f?refox", "firefox", true)]
        [InlineData("f?refox", "ffirefox", false)]
        [InlineData("*", "anything", true)]
        public void GlobMatches_backtracks_correctly(string pattern, string text, bool expected)
        {
            Assert.Equal(expected, RuleText.GlobMatches(pattern, text));
        }

        [Fact]
        public void GlobMatches_is_case_sensitive_by_design()
        {
            // Case folding happens once in NameMatches, not in the matcher.
            Assert.False(RuleText.GlobMatches("firefox", "FireFox"));
        }

        [Theory]
        [InlineData("firefox", "firefox", true)]
        [InlineData("FireFox", "firefox", true)]
        [InlineData("firefox", "  FIREFOX  ", true)]
        [InlineData("chrom*", "Chromium", true)]
        [InlineData("firefox", "discord", false)]
        [InlineData("", "firefox", false)]
        [InlineData("firefox", "", false)]
        public void NameMatches_is_case_insensitive_and_trims(string pattern, string candidate, bool expected)
        {
            Assert.Equal(expected, RuleText.NameMatches(pattern, candidate));
        }

        [Fact]
        public void NameMatches_compensates_for_kernel_comm_truncation()
        {
            // Linux caps comm at 15 characters: 'chromium-browser' becomes
            // 'chromium-browse'. A stricter comparison would silently never match.
            Assert.Equal(15, "chromium-browse".Length);

            Assert.True(RuleText.NameMatches("chromium-browser", "chromium-browse"));
        }

        [Fact]
        public void Truncation_compensation_does_not_invent_matches()
        {
            // The candidate must be exactly at the cap and be a real prefix.
            Assert.False(RuleText.NameMatches("chromium-browser", "chromium"));
            Assert.False(RuleText.NameMatches("chromium-browser", "firefox-browser"));
        }

        [Fact]
        public void Truncation_compensation_is_skipped_for_glob_patterns()
        {
            // Truncation plus wildcards cannot both be reasoned about precisely,
            // so the matcher declines to guess.
            Assert.False(RuleText.NameMatches("chromium-browser*", "chromium-browse"));
            // ...while the glob still matches a full-length name.
            Assert.True(RuleText.NameMatches("chromium-browser*", "chromium-browser"));
        }

        [Fact]
        public void Describe_elides_beyond_the_cap()
        {
            var values = new List<string> { "a", "b", "c", "d" };

            Assert.Equal("a, b, +2 more", RuleText.Describe(values, 2));
            Assert.Equal("a, b, c, d", RuleText.Describe(values, 10));
            Assert.Equal("(none)", RuleText.Describe(new string[0], 3));
            Assert.Equal("(none)", RuleText.Describe(null, 3));
        }
    }
}
