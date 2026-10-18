using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class FocusStatusTests
    {
        [Fact]
        public void Apply_merges_the_flat_payload()
        {
            var status = FocusStatusMapper.Apply(new FocusStatus(), new FocusStateResponse
            {
                ok = true,
                frozen_count = 1,
                version = "0.1.0",
            });
            var previousCount = status.FrozenCount;

            var response = new FocusStateResponse
            {
                ok = true,
                active = true,
                dry_run = true,
                degraded = true,
                frozen_count = 3,
                frozen_total = 9,
                frozen_names = new[] { "firefox" },
                recent_names = new[] { "discord" },
                version = "0.2.0",
            };

            status = FocusStatusMapper.Apply(status, response);

            Assert.Equal(1, previousCount);
            Assert.True(status.Connected);
            Assert.True(status.DryRun);
            Assert.True(status.Degraded);
            Assert.True(status.SessionActive);
            Assert.Equal(3, status.FrozenCount);
            Assert.Equal(9, status.FrozenTotal);
            Assert.Equal("firefox", status.FrozenNames[0]);
            Assert.Equal("discord", status.RecentNames[0]);
            Assert.Equal("0.2.0", status.Version);
        }

        [Fact]
        public void Apply_returns_a_new_snapshot_and_leaves_the_old_one_alone()
        {
            var before = FocusStatusMapper.Apply(new FocusStatus(), new FocusStateResponse
            {
                ok = true,
                frozen_count = 1,
            });

            var after = FocusStatusMapper.Apply(before, new FocusStateResponse
            {
                ok = true,
                frozen_count = 4,
            });

            Assert.NotSame(before, after);
            Assert.Equal(1, before.FrozenCount);
            Assert.Equal(4, after.FrozenCount);
        }

        [Fact]
        public void Apply_treats_missing_arrays_as_empty()
        {
            var status = FocusStatusMapper.Apply(new FocusStatus(), new FocusStateResponse { ok = true });

            Assert.Empty(status.FrozenNames);
            Assert.Empty(status.RecentNames);
        }
    }
}
