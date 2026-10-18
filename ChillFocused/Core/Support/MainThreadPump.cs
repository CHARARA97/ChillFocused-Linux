using System;
using System.Threading;

namespace ChillFocused.Core
{
    /// <summary>
    /// Captures Unity's main-thread context and posts work to it.
    /// </summary>
    /// <remarks>
    /// The plugin object can be destroyed while the game continues, so work that
    /// needs Unity must be queued through the context rather than relying on the
    /// plugin's Update. This class owns the context, counters and failure handling;
    /// the entry point just posts a callback.
    /// </remarks>
    internal sealed class MainThreadPump
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private SynchronizationContext _context;
        private int _posts;
        private int _runs;
        private bool _loggedFirstRun;

        internal MainThreadPump(Action<string> logInfo, Action<string> logWarning)
        {
            _logInfo = logInfo ?? (message => { });
            _logWarning = logWarning ?? (message => { });
        }

        internal int Posts
        {
            get { return Volatile.Read(ref _posts); }
        }

        internal int Runs
        {
            get { return Volatile.Read(ref _runs); }
        }

        internal bool Install()
        {
            try
            {
                _context = SynchronizationContext.Current;
                _logInfo(LogEvent.Line("main_thread.context",
                    "thread=" + Thread.CurrentThread.ManagedThreadId,
                    "context=" + (_context == null ? "null" : _context.GetType().FullName)));
            }
            catch (Exception ex)
            {
                _logWarning(LogEvent.Line("main_thread.failed", "reason=" + ex.Message));
                return false;
            }

            if (_context == null)
            {
                _logWarning(LogEvent.Line("main_thread.missing"));
                return false;
            }

            return true;
        }

        internal void Post(Action work)
        {
            var context = _context;
            if (context == null || work == null)
            {
                return;
            }

            Interlocked.Increment(ref _posts);
            try
            {
                context.Post(
                    _ =>
                    {
                        Interlocked.Increment(ref _runs);
                        if (!_loggedFirstRun)
                        {
                            _loggedFirstRun = true;
                            _logInfo(LogEvent.Line("main_thread.first_run", "posts=" + Posts));
                        }

                        try
                        {
                            work();
                        }
                        catch (Exception ex)
                        {
                            _logWarning(LogEvent.Line("main_thread.work.failed", "reason=" + ex.Message));
                        }
                    },
                    null);
            }
            catch (Exception ex)
            {
                _logWarning(LogEvent.Line("main_thread.post.failed", "reason=" + ex.Message));
            }
        }
    }
}
