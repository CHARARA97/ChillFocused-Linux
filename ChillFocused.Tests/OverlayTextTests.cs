using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class OverlayTextTests
    {
        [Fact]
        public void TextFor_strips_the_warning_marker()
        {
            Assert.Equal("offline", OverlayText.TextFor("!offline"));
            Assert.Equal("fine", OverlayText.TextFor("fine"));
        }

        [Fact]
        public void TextFor_treats_missing_text_as_empty()
        {
            Assert.Equal(string.Empty, OverlayText.TextFor(null));
            Assert.Equal(string.Empty, OverlayText.TextFor(string.Empty));
        }

        [Fact]
        public void TextFor_only_strips_a_leading_marker()
        {
            Assert.Equal("a!b", OverlayText.TextFor("a!b"));
            Assert.Equal("!b", OverlayText.TextFor("!!b"));
        }

        [Fact]
        public void AddWarn_marks_the_line_it_appends()
        {
            var lines = new List<string>();

            OverlayText.AddWarn(lines, "cannot reach the executor");

            Assert.Single(lines);
            Assert.Equal("!cannot reach the executor", lines[0]);
            Assert.Equal("cannot reach the executor", OverlayText.TextFor(lines[0]));
        }

        [Fact]
        public void AddWarn_does_nothing_without_a_list()
        {
            OverlayText.AddWarn(null, "ignored");
        }

        [Fact]
        public void Clip_leaves_text_within_the_limit_alone()
        {
            Assert.Equal("abc", OverlayText.Clip("abc", 3));
            Assert.Equal("abc", OverlayText.Clip("abc", 20));
        }

        [Fact]
        public void Clip_marks_what_it_removed()
        {
            var result = OverlayText.Clip("abcdefghij", 5);

            Assert.Equal(5, result.Length);
            Assert.StartsWith("abcd", result);
            Assert.EndsWith("\u2026", result);
        }

        [Fact]
        public void Clip_returns_empty_for_missing_text_or_no_room()
        {
            Assert.Equal(string.Empty, OverlayText.Clip(null, 10));
            Assert.Equal(string.Empty, OverlayText.Clip("abcdef", 0));
        }
    }
}
