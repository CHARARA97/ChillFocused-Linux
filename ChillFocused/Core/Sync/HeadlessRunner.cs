using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Text;
using System.Threading;

namespace ChillFocused.Core
{
    /// <summary>
    /// The part of the plugin that must work no matter what Unity does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On this game under Proton it turned out that nothing created while BepInEx
    /// runs its chainloader ever receives a frame callback: the objects are
    /// destroyed before their first <c>Start</c>, and neither <c>Update</c>,
    /// <c>OnGUI</c>, <c>Application.onBeforeRender</c> nor a Harmony patch on
    /// <c>Canvas.SendWillRenderCanvases</c> ever fires afterwards. Only
    /// <c>Awake</c> and <c>OnDestroy</c> are delivered.
    /// </para>
    /// <para>
    /// So the job of keeping Focused armed is done here, on an ordinary
    /// background thread, using only <see cref="HttpTransport"/> -- which is
    /// proven to work from inside the prefix. Nothing in this file touches
    /// UnityEngine, which also means it is compiled into the test project and
    /// covered by unit tests.
    /// </para>
    /// <para>
    /// The overlay is still attempted by the plugin when Unity does tick; it is a
    /// bonus, not a dependency.
    /// </para>
    /// </remarks>
    internal sealed class HeadlessRunner : IDisposable
    {
        private readonly Func<FocusSettings> _settingsProvider;
        private readonly HttpTransport _transport;
        private readonly double _intervalSeconds;
        private readonly Action<string> _log;
        private readonly Action<string> _warn;
        private readonly object _gate = new object();

        private Thread _thread;
        private volatile bool _stop;

        private readonly FocusStatusProbe _probe;
        private readonly RulePublisher _rules;
        private readonly SessionReporter _reporter;
        private readonly ISessionReportGate _reportGate;
        private string _lastLoggedError;
        private bool _everConnected;
        private bool _connected;

        private int _cycles;
        private int _failures;
        private float _lastHeartbeatAt;
        private readonly PerfCounter _cycleTiming = new PerfCounter();

        public HeadlessRunner(
            Func<FocusSettings> settingsProvider,
            HttpTransport transport,
            FocusStatusProbe probe,
            RulePublisher rules,
            SessionReporter reporter,
            double intervalSeconds,
            Action<string> log,
            Action<string> warn,
            ISessionReportGate reportGate = null)
        {
            _settingsProvider = settingsProvider ?? (() => new FocusSettings());
            _transport = transport ?? new HttpTransport(null, null, 0);
            _probe = probe ?? new FocusStatusProbe(() => null);
            _rules = rules ?? new RulePublisher(body => { }, warn, log);
            _reporter = reporter ?? new SessionReporter(body => { });
            _reportGate = reportGate;
            _intervalSeconds = intervalSeconds < 0.5 ? 0.5 : intervalSeconds;
            _log = log ?? (message => { });
            _warn = warn ?? (message => { });
            GameStateProvider = () => default(TimerState);
            OnCycle = () => { };
        }

        public bool Connected
        {
            get { lock (_gate) { return _connected; } }
        }

        public int PushCount
        {
            get { return _rules.PushCount; }
        }

        public int CycleCount
        {
            get { lock (_gate) { return _cycles; } }
        }

        public string LastError
        {
            get { lock (_gate) { return _lastLoggedError; } }
        }

        public string BaseUrl
        {
            get { return _transport.BaseUrl; }
        }

        /// <summary>
        /// Invoked at the end of every cycle, on this background thread.
        /// </summary>
        /// <remarks>
        /// Used by the plugin to queue work onto Unity's main thread. Posting
        /// from here rather than from inside a posted callback matters: Unity
        /// drains its queue in a loop, so a self-reposting callback could spin
        /// forever within a single frame and hang the game.
        /// </remarks>
        public Action OnCycle;

        /// <summary>
        /// Where the game's timer state comes from. A delegate rather than a direct
        /// call so this class keeps compiling without the game, Unity or Harmony.
        /// </summary>
        internal Func<TimerState> GameStateProvider;

        /// <summary>
        /// Set to run a cycle immediately instead of waiting for the next tick.
        /// </summary>
        /// <remarks>
        /// Used when the game's timer changes. This thread owns its own synchronous
        /// transport, so waking it cannot collide with the overlay's polling the way
        /// a second request on that client would.
        /// </remarks>
        private readonly System.Threading.AutoResetEvent _wake =
            new System.Threading.AutoResetEvent(false);

        /// <summary>Last timer state pushed, so a transition is logged only once.</summary>
        private bool? _lastReportedWorking;

        /// <summary>
        /// Fire a one-shot request from the thread pool.
        /// </summary>
        /// <remarks>
        /// Used for the few controls in the advanced panel that are not part of the
        /// regular sync cycle. Deliberately not routed through the overlay's client,
        /// whose single-in-flight guard would drop the request whenever a poll
        /// happened to be running. HttpTransport opens a connection per call and
        /// holds only immutable state, so this cannot race with the sync loop.
        /// </remarks>
        internal void Fire(string method, string path, string body)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    _transport.Request(method, path, body);
                }
                catch (Exception ex)
                {
                    Report("one-shot " + path + " failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            });
        }

        /// <summary>Report the game's timer state on the next instant, not the next tick.</summary>
        internal void Wake()
        {
            try
            {
                _wake.Set();
            }
            catch (ObjectDisposedException)
            {
                // Shutting down; nothing to report.
            }
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "ChillFocused-sync"
            };
            _thread.Start();
            _log(LogEvent.Line("sync.start",
                 "url=" + _transport.BaseUrl,
                 "interval=" + _intervalSeconds.ToString("0.##")));
        }

        public void Stop()
        {
            _stop = true;
        }

        public void Dispose()
        {
            Stop();
        }

        // ------------------------------------------------------------------

        private void Loop()
        {
            while (!_stop)
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    Cycle();
                }
                catch (Exception ex)
                {
                    // Cycle() already handles its own errors; this is belt and
                    // braces so the thread can never die quietly.
                    Report(ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    _cycleTiming.Record(
                        (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                }

                var hook = OnCycle;
                if (hook != null)
                {
                    try
                    {
                        hook();
                    }
                    catch (Exception)
                    {
                        // The hook is best-effort; never stop syncing because of it.
                    }
                }

                var deadline = DateTime.UtcNow.AddSeconds(_intervalSeconds);
                while (!_stop && DateTime.UtcNow < deadline)
                {
                    if (_wake.WaitOne(100))
                    {
                        break;   // a timer transition wants reporting now
                    }
                }
            }

            _log(LogEvent.Line("sync.stop"));
        }

        internal SyncDebugSnapshot CaptureDebug(float now)
        {
            lock (_gate)
            {
                return new SyncDebugSnapshot
                {
                    Connected = _connected,
                    RulesDue = _rules.IsDue(now),
                    BackendPid = _rules.BackendPid,
                    Cycles = _cycles,
                    Pushes = _rules.PushCount,
                    LastHeartbeatAt = _lastHeartbeatAt,
                    LastCycleMs = _cycleTiming.LastMs,
                    MaxCycleMs = _cycleTiming.MaxMs,
                    LastError = _lastLoggedError,
                    RuleLastError = _rules.LastError,
                };
            }
        }

        internal void Cycle()
        {
            var settings = _settingsProvider() ?? new FocusSettings();
            var now = TimeSinceStart();

            // A state call doubles as the connectivity probe and as the way to
            // learn what Focused currently believes.
            var status = _probe.Read();

            lock (_gate)
            {
                _cycles++;
                _connected = true;
                _lastLoggedError = null;
            }

            if (!_everConnected)
            {
                _everConnected = true;
                _log(LogEvent.Line("sync.connected",
                     "session=" + (status.Active.HasValue ? (status.Active.Value ? "on" : "off") : "unknown")));
            }

            // Rules and heartbeat are independent channels. A rejected rule set
            // must never prevent the lease from being renewed (PLG-02).
            _rules.Publish(settings, now, status.Pid);

            // This thread is the reliable reporter: it keeps running even if the
            // overlay object is destroyed, and it renews the lease that keeps
            // Focused from resuming everything when the plugin stops existing.
            var provider = GameStateProvider;
            var timer = provider != null ? provider() : default(TimerState);
            var canReport = _reportGate == null || _reportGate.CanReport;
            if (canReport)
            {
                var active = _reporter.Report(settings, timer, now);
                _lastHeartbeatAt = now;

                if (_lastReportedWorking != active)
                {
                    _lastReportedWorking = active;
                    _log(LogEvent.Line("session.change",
                         "timer=" + (timer.Working ? "running" : "stopped"),
                         "session=" + (active ? "on" : "off")));
                }
            }
            else if (_reportGate.TryConsumeWithdrawal())
            {
                // Withdraw the previous game-timer claim once, then stay quiet while
                // the external session owns the game timer.
                _reporter.Report(settings, timer, now, false);
            }
        }

        /// <summary>
        /// Seconds on the same monotonic clock the timer probe stamps into
        /// <c>TimerState.ReadAt</c>.
        /// </summary>
        private float TimeSinceStart()
        {
            // Shared with GameTimerProbe, so ReadAt and now are comparable.
            return GameClock.Now;
        }

        /// <summary>The source label is internal, but keep the payload well-formed.</summary>
        private static string JsonSource(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "plugin"
                : value.Replace("\\", "/").Replace("\"", string.Empty);
        }

        private void Report(string message)
        {
            lock (_gate)
            {
                _failures++;
                if (string.Equals(_lastLoggedError, message, StringComparison.Ordinal))
                {
                    return;
                }

                _lastLoggedError = message;
                _connected = false;
            }

            // Log the first failure and then only when the reason changes, so a
            // Focused that is simply not running cannot flood the log.
            _warn(LogEvent.Line("sync.failed",
                "url=" + _transport.BaseUrl, "reason=" + message));
        }

    }
}
