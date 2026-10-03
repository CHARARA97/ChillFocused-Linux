using System;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class TextFitTests
    {
        /// <summary>A measurer where one character costs ten pixels.</summary>
        private static float TenPerCharacter(string text)
        {
            return (text ?? string.Empty).Length * 10f;
        }

        [Fact]
        public void Shorten_leaves_text_within_the_limit_alone()
        {
            Assert.Equal("hello", TextFit.Shorten("hello", 5));
            Assert.Equal("hello", TextFit.Shorten("hello", 40));
        }

        [Fact]
        public void Shorten_marks_what_it_removed()
        {
            var result = TextFit.Shorten("abcdefghij", 5);

            Assert.True(result.Length <= 5, result);
            Assert.StartsWith("abcd", result);
            Assert.EndsWith(TextFit.Ellipsis, result);
        }

        [Fact]
        public void Shorten_degrades_to_the_ellipsis_alone()
        {
            Assert.Equal(TextFit.Ellipsis, TextFit.Shorten("abcdef", 1));
            Assert.Equal(string.Empty, TextFit.Shorten("abcdef", 0));
        }

        [Fact]
        public void Shorten_treats_missing_text_as_empty()
        {
            Assert.Equal(string.Empty, TextFit.Shorten(null, 10));
            Assert.Equal(string.Empty, TextFit.Shorten(string.Empty, 10));
        }

        [Fact]
        public void Fit_leaves_text_that_already_fits_alone()
        {
            Assert.Equal("abc", TextFit.Fit("abc", 30f, TenPerCharacter));
            Assert.Equal("abc", TextFit.Fit("abc", 1000f, TenPerCharacter));
        }

        [Fact]
        public void Fit_shortens_until_it_measures_within_the_width()
        {
            var result = TextFit.Fit("abcdefghij", 45f, TenPerCharacter);

            Assert.True(TenPerCharacter(result) <= 45f, result);
            Assert.EndsWith(TextFit.Ellipsis, result);
        }

        [Fact]
        public void Fit_falls_back_to_the_ellipsis_when_nothing_fits()
        {
            Assert.Equal(TextFit.Ellipsis, TextFit.Fit("abcdef", 5f, TenPerCharacter));
        }

        [Fact]
        public void Fit_keeps_the_text_when_it_cannot_measure()
        {
            // A style that throws must not blank the panel; the untouched text is the
            // least surprising outcome and the caller still has Overflow clipping.
            Assert.Equal("abcdef", TextFit.Fit("abcdef", 5f, null));
            Assert.Equal("abcdef", TextFit.Fit("abcdef", 5f, delegate { throw new InvalidOperationException(); }));
        }
    }
}
