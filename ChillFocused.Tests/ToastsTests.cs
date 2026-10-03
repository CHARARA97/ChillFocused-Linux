using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class ToastsTests
    {
        private static Toasts WithOne(float now, float seconds, string text = "hello", bool warning = false)
        {
            var toasts = new Toasts();
            toasts.Add(text, warning, now, seconds, Toasts.MaxEntries);
            return toasts;
        }

        [Fact]
        public void A_new_queue_is_empty()
        {
            var toasts = new Toasts();

            Assert.Equal(0, toasts.Count);
            Assert.Empty(toasts.Items);
        }

        [Fact]
        public void Add_keeps_the_text_and_the_warning_flag()
        {
            var toasts = new Toasts();

            toasts.Add("cannot reach the executor", true, 10f, 4f, Toasts.MaxEntries);
            toasts.Add("connected", false, 10f, 4f, Toasts.MaxEntries);

            Assert.Equal(2, toasts.Count);
            Assert.Equal("cannot reach the executor", toasts.Items[0].Text);
            Assert.True(toasts.Items[0].Warning);
            Assert.Equal("connected", toasts.Items[1].Text);
            Assert.False(toasts.Items[1].Warning);
        }

        [Fact]
        public void Add_expires_the_toast_the_requested_seconds_after_now()
        {
            var toasts = WithOne(100f, 4f);

            Assert.Equal(104f, toasts.Items[0].ExpiresAt);
        }

        [Fact]
        public void Add_keeps_a_toast_on_screen_for_at_least_a_second()
        {
            Assert.Equal(1f, WithOne(0f, 0f).Items[0].ExpiresAt);
            Assert.Equal(1f, WithOne(0f, -3f).Items[0].ExpiresAt);
            Assert.Equal(1f, WithOne(0f, 0.2f).Items[0].ExpiresAt);
        }

        [Fact]
        public void Add_treats_missing_text_as_empty()
        {
            var toasts = new Toasts();

            toasts.Add(null, false, 0f, 4f, Toasts.MaxEntries);

            Assert.Equal(string.Empty, toasts.Items[0].Text);
        }

        [Fact]
        public void Add_drops_the_oldest_toasts_once_the_cap_is_reached()
        {
            var toasts = new Toasts();
            for (var i = 0; i < 6; i++)
            {
                toasts.Add("t" + i, false, i, 4f, 4);
            }

            Assert.Equal(4, toasts.Count);
            Assert.Equal("t2", toasts.Items[0].Text);
            Assert.Equal("t5", toasts.Items[3].Text);
        }

        [Fact]
        public void Add_never_grows_past_a_cap_below_one()
        {
            var toasts = new Toasts();

            toasts.Add("a", false, 0f, 4f, 0);
            toasts.Add("b", false, 0f, 4f, -5);

            Assert.Equal(1, toasts.Count);
            Assert.Equal("b", toasts.Items[0].Text);
        }

        [Fact]
        public void Expire_drops_what_the_clock_has_reached()
        {
            var toasts = new Toasts();
            toasts.Add("short", false, 0f, 1f, Toasts.MaxEntries);
            toasts.Add("long", false, 0f, 5f, Toasts.MaxEntries);

            toasts.Expire(1f);

            Assert.Equal(1, toasts.Count);
            Assert.Equal("long", toasts.Items[0].Text);
        }

        [Fact]
        public void Expire_keeps_everything_still_alive()
        {
            var toasts = WithOne(0f, 4f);

            toasts.Expire(3.9f);

            Assert.Equal(1, toasts.Count);
        }

        [Fact]
        public void Expire_survives_an_empty_queue()
        {
            var toasts = new Toasts();

            toasts.Expire(9f);

            Assert.Equal(0, toasts.Count);
        }

        [Fact]
        public void Alpha_is_opaque_until_the_fade_window()
        {
            var toasts = WithOne(0f, 4f);

            Assert.Equal(1f, Toasts.Alpha(toasts.Items[0], 0f), 3);
            Assert.Equal(1f, Toasts.Alpha(toasts.Items[0], 4f - Toasts.FadeSeconds), 3);
        }

        [Fact]
        public void Alpha_fades_to_zero_at_expiry()
        {
            var toasts = WithOne(0f, 4f);

            Assert.Equal(0.5f, Toasts.Alpha(toasts.Items[0], 4f - Toasts.FadeSeconds / 2f), 3);
            Assert.Equal(0f, Toasts.Alpha(toasts.Items[0], 4f));
        }

        [Fact]
        public void Alpha_never_goes_below_zero()
        {
            var toasts = WithOne(0f, 4f);

            Assert.Equal(0f, Toasts.Alpha(toasts.Items[0], 99f));
        }

        [Fact]
        public void Alpha_of_a_missing_toast_is_nothing()
        {
            Assert.Equal(0f, Toasts.Alpha(null, 0f));
        }
    }
}
