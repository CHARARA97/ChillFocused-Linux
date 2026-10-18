using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// The read-only side of the Focused API, as the poller needs it.
    /// </summary>
    /// <remarks>
    /// Keeping this narrow lets the poller be tested with a fake feed, and keeps
    /// the overlay from knowing how the HTTP client is implemented.
    /// </remarks>
    public interface IStatusFeed
    {
        bool Busy { get; }
        void GetFocusStateAsync(Action<FocusStateResponse, string> done);
        void GetEventsAsync(int since, int limit, Action<EventListResponse, string> done);
    }

    /// <summary>
    /// Schedules the overlay's status polls and event cursor.
    /// </summary>
    /// <remarks>
    /// This used to live inline in <c>FocusRunner</c>, mixed with HUD and panel
    /// code. It has no Unity types and no knowledge of what the callbacks draw, so
    /// the timing/cursor behaviour can be tested without the game.
    /// </remarks>
    public sealed class StatusPoller
    {
        private const int MaxEvents = 100;
        private IStatusFeed _feed;
        private float _nextPollAt;
        private int _eventCursor = -1;
        private readonly PerfCounter _requests = new PerfCounter();

        public void SetFeed(IStatusFeed feed)
        {
            _feed = feed;
            _nextPollAt = 0f;
            _eventCursor = -1;
        }

        public void ResetEvents()
        {
            _eventCursor = -1;
        }

        public int EventCursor
        {
            get { return _eventCursor; }
        }

        public float NextPollAt
        {
            get { return _nextPollAt; }
        }

        public int RequestCount
        {
            get { return _requests.Count; }
        }

        public double LastRequestMs
        {
            get { return _requests.LastMs; }
        }

        public double MaxRequestMs
        {
            get { return _requests.MaxMs; }
        }

        /// <summary>Start consuming events after a successful connection.</summary>
        public void BeginEvents()
        {
            _eventCursor = 0;
        }

        /// <summary>
        /// Poll if due. The callbacks run on the client's delivery thread, which
        /// <see cref="FocusedClient"/> guarantees is the Unity main thread.
        /// </summary>
        public void Poll(float now, float interval,
                         Action<FocusStateResponse, string> onStatus,
                         Action<EventListResponse> onEvents)
        {
            if (_feed == null || _feed.Busy)
            {
                return;
            }

            interval = interval < 0.2f ? 0.2f : interval;
            if (now < _nextPollAt)
            {
                return;
            }

            _nextPollAt = now + interval;
            var feed = _feed;
            var started = GameClock.Now;
            feed.GetFocusStateAsync(delegate(FocusStateResponse status, string error)
            {
                _requests.Record((GameClock.Now - started) * 1000.0);
                if (onStatus != null)
                {
                    onStatus(status, error);
                }

                if (error != null || status == null || !status.ok || _eventCursor < 0)
                {
                    return;
                }

                feed.GetEventsAsync(_eventCursor, MaxEvents, delegate(EventListResponse events, string eventError)
                {
                    if (eventError != null || events == null || !events.ok)
                    {
                        return;
                    }

                    _eventCursor = events.next_seq;
                    if (onEvents != null)
                    {
                        onEvents(events);
                    }
                });
            });
        }
    }
}
