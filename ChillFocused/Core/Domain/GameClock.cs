using System.Diagnostics;

namespace ChillFocused.Core
{
    /// <summary>
    /// One monotonic clock shared by the timer probe and the sync thread.
    /// </summary>
    /// <remarks>
    /// The game's timer state and the session-freshness window used to be stamped
    /// with two different clocks (Unity unscaled time vs. a DateTime started later).
    /// That made a reading look older than it was by the gap between process start
    /// and plugin load.  Both sides now read this single Stopwatch.
    /// </remarks>
    internal static class GameClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static float Now
        {
            get { return (float)Watch.Elapsed.TotalSeconds; }
        }
    }
}
