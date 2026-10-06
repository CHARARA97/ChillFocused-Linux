using System;
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
    public sealed class HeadlessRunner : IDisposable
    {
        private readonly Func<FocusSettings> _settingsProvider;
        private readonly HttpTransport _transport;
        private readonly double _intervalSeconds;
        private readonly Action<string> _log;
        private readonly Action<string> _warn;
        private readonly object _gate = new object();

        private Thread _thread;
        private volatile bool _stop;

        private string _lastRulesJson;
        private DateTime _lastPushedUtc = DateTime.MinValue;

        /// <summary>How often to re-send the rules even when they have not changed.</summary>
        /// <remarks>
        /// Comparing the payload alone silently skipped the push when Focused had
        /// restarted: the plugin still held the previous text, decided nothing had
        /// changed, and the protect list from this config stayed unapplied.
        /// </remarks>
        private static readonly TimeSpan RuleRefreshInterval = TimeSpan.FromSeconds(30);
        private string _lastLoggedError;
        private bool _everConnected;
        private bool _connected;

        private int _cycles;
        private int _pushes;
        private int _failures;

        public HeadlessRunner(
            Func<FocusSettings> settingsProvider,
            string baseUrl,
            string token,
            int timeoutMs,
            double intervalSeconds,
            Action<string> log,
            Action<string> warn)
        {
            _settingsProvider = settingsProvider ?? (() => new FocusSettings());
            _transport = new HttpTransport(baseUrl, token, timeoutMs);
            _intervalSeconds = intervalSeconds < 0.5 ? 0.5 : intervalSeconds;
            _log = log ?? (message => { });
            _warn = warn ?? (message => { });
        }

        /// <summary>The headless runner's last session decision.</summary>
        /// <remarks>
        /// The headless runner owns the session heartbeat: it always runs, while the
        /// Unity-side component can be destroyed and recreated by a scene load (it was,
        /// repeatedly, on this game).  Two owners of one lease is a race -- the two
        /// heartbeats overwrote each other every couple of seconds and the session
        /// flapped on and off -- so the Unity side mirrors this value instead of
        /// sending its own.
        /// </remarks>
        internal static bool SessionActive { get; private set; }

        public bool Connected
        {
            get { lock (_gate) { return _connected; } }
        }

        public int PushCount
        {
            get { lock (_gate) { return _pushes; } }
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
            _log("headless sync thread started; Focused " + _transport.BaseUrl +
                 ", interval " + _intervalSeconds.ToString("0.##") + "s");
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

            _log("headless sync thread stopped");
        }

        private readonly DateTime _startedUtc = DateTime.UtcNow;

        private void Cycle()
        {
            var settings = _settingsProvider() ?? new FocusSettings();
            var rulesJson = settings.BuildRulesJson();

            // A state call doubles as the connectivity probe and as the way to
            // learn what Focused currently believes.
            var state = _transport.Request("GET", "/api/v1/focus", null);
            var focusActive = ReadBool(state, "active");

            lock (_gate)
            {
                _cycles++;
                _connected = true;
                _lastLoggedError = null;
            }

            if (!_everConnected)
            {
                _everConnected = true;
                _log("connected to Focused" +
                     (focusActive.HasValue ? " (session " + (focusActive.Value ? "ON" : "OFF") + ")" : string.Empty));
            }

            // Push this plugin's rules whenever they differ from what we last sent.
            if (rulesJson != _lastRulesJson
                || DateTime.UtcNow - _lastPushedUtc >= RuleRefreshInterval)
            {
                _transport.Request("PUT", "/api/v1/focus/rules", rulesJson);
                _lastRulesJson = rulesJson;
                _lastPushedUtc = DateTime.UtcNow;
                lock (_gate) { _pushes++; }
                _log("pushed rules: " + DescribeRules(settings));
            }

            // Report the session, with a lease. This thread is the reliable
            // reporter: it keeps running even if the overlay object is destroyed,
            // and it is what renews the lease that keeps Focused from resuming
            // everything the moment the plugin stops existing.
            var provider = GameStateProvider;
            var timer = provider != null ? provider() : default(TimerState);
            var now = TimeSinceStart();
            var active = settings.SessionActive(timer, now);
            SessionActive = active;
            var body =
                "{\"active\":" + (active ? "true" : "false") +
                ",\"dry_run\":" + (settings.DryRun ? "true" : "false") +
                ",\"source\":\"game-timer\"" +
                ",\"ttl_seconds\":" + settings.SessionLeaseSeconds + "}";
            _transport.Request("POST", "/api/v1/focus/state", body);

            if (_lastReportedWorking != active)
            {
                _lastReportedWorking = active;
                _log("the game's timer is " + (timer.Working ? "running" : "stopped") +
                     " -> session " + (active ? "ON" : "OFF"));
            }
        }

        /// <summary>
        /// Unscaled seconds since this runner started.
        /// </summary>
        /// <remarks>
        /// The session window is measured against the *reading's* clock, which the
        /// game-time probe stamps with Unity's unscaled time. On this thread there is
        /// no frame, so the plugin's own monotonic-ish clock is used; both count
        /// seconds since process start, which is what the window compares.
        /// </remarks>
        private float TimeSinceStart()
        {
            return (float)(DateTime.UtcNow - _startedUtc).TotalSeconds;
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
            _warn("cannot reach " + _transport.BaseUrl + ": " + message);
        }

        private static string DescribeRules(FocusSettings settings)
        {
            return RuleText.Describe(settings.ProcessNames, 8) + " (names), " +
                   RuleText.Describe(settings.CmdlineSubstrings, 8) + " (cmdline)";
        }

        /// <summary>
        /// Pull a boolean out of a JSON response without a JSON parser.
        /// </summary>
        /// <remarks>
        /// Deliberately crude: the only fields read here are Focused's own
        /// <c>enabled</c> flags, and <c>JsonUtility</c> is off limits because this
        /// runs on a background thread. A miss just means "unknown", which the
        /// caller treats as "do not correct anything".
        /// </remarks>
        internal static bool? ReadBool(string json, string key)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var needle = "\"" + key + "\"";
            var index = json.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                var after = index + needle.Length;
                while (after < json.Length && (json[after] == ' ' || json[after] == ':'))
                {
                    after++;
                }

                if (after < json.Length)
                {
                    if (string.CompareOrdinal(json, after, "true", 0, 4) == 0)
                    {
                        return true;
                    }

                    if (string.CompareOrdinal(json, after, "false", 0, 5) == 0)
                    {
                        return false;
                    }
                }

                index = json.IndexOf(needle, index + 1, StringComparison.Ordinal);
            }

            return null;
        }

        internal static string BuildEnabledJson(bool enabled)
        {
            return enabled ? "{\"enabled\":true}" : "{\"enabled\":false}";
        }
    }

    /// <summary>Serialises a rule set to Focused's wire format by hand.</summary>
    internal static class RuleJson
    {
        public static string Build(FocusSettings settings)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"names\":");
            AppendArray(builder, settings.ProcessNames);
            builder.Append(",\"cmdline_substrings\":");
            AppendArray(builder, settings.CmdlineSubstrings);
            builder.Append(",\"pids\":[],\"pid_guards\":{},\"protect_names\":");
            AppendArray(builder, settings.ProtectedNames);
            builder.Append(",\"protect_cmdline_substrings\":");
            AppendArray(builder, settings.ProtectedCmdlineSubstrings);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendArray(StringBuilder builder, string[] values)
        {
            builder.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendString(builder, values[i]);
                }
            }

            builder.Append(']');
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (ch < 0x20)
                        {
                            builder.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
