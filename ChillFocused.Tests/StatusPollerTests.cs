using System;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The overlay's read-only polling schedule, isolated from Unity and HTTP.
    /// </summary>
    public class StatusPollerTests
    {
        private sealed class FakeFeed : IStatusFeed
        {
            public bool Busy { get; set; }
            public int StateCalls;
            public int EventCalls;
            public bool StatusOk = true;
            public EventListResponse Events = new EventListResponse { ok = true, next_seq = 7 };

            public void GetFocusStateAsync(Action<FocusStateResponse, string> done)
            {
                StateCalls++;
                done(new FocusStateResponse { ok = StatusOk }, null);
            }

            public void GetEventsAsync(int since, int limit, Action<EventListResponse, string> done)
            {
                EventCalls++;
                done(Events, null);
            }
        }

        [Fact]
        public void A_successful_poll_reads_status_then_events()
        {
            var feed = new FakeFeed();
            var poller = new StatusPoller();
            poller.SetFeed(feed);
            poller.BeginEvents();
            var statuses = 0;
            var events = 0;

            poller.Poll(0f, 1f,
                delegate(FocusStateResponse state, string error) { statuses++; },
                delegate(EventListResponse list) { events++; });

            Assert.Equal(1, statuses);
            Assert.Equal(1, events);
            Assert.Equal(1, feed.StateCalls);
            Assert.Equal(1, feed.EventCalls);
        }

        [Fact]
        public void A_failed_status_does_not_read_events()
        {
            var feed = new FakeFeed { StatusOk = false };
            var poller = new StatusPoller();
            poller.SetFeed(feed);
            poller.BeginEvents();

            poller.Poll(0f, 1f, null, null);

            Assert.Equal(0, feed.EventCalls);
        }

        [Fact]
        public void The_interval_and_busy_flag_suppress_duplicate_polls()
        {
            var feed = new FakeFeed();
            var poller = new StatusPoller();
            poller.SetFeed(feed);
            poller.BeginEvents();

            poller.Poll(0f, 5f, null, null);
            poller.Poll(2f, 5f, null, null);
            Assert.Equal(1, feed.StateCalls);

            feed.Busy = true;
            poller.Poll(10f, 5f, null, null);
            Assert.Equal(1, feed.StateCalls);
        }
    }
}
