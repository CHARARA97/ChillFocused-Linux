using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// JSON-over-loopback client for <b>Focused</b>, the backend application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three rules shape this class:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Never touch the Unity main thread from a worker.</b> Requests run on
    /// the thread pool; results are queued as closures and only ever executed
    /// from <see cref="Pump"/>, which the plugin calls in
    /// <c>Update()</c>.
    /// </description></item>
    /// <item><description>
    /// <b>One request in flight.</b> The Focused is a status feed, not a message
    /// bus; a queue of stale polls helps nobody. A second request while one is
    /// running is dropped with an error handed to the caller, on the main
    /// thread like every other callback.
    /// </description></item>
    /// <item><description>
    /// <b>Assume Mono's HTTP stack can wedge.</b> A watchdog clears the
    /// in-flight flag if a request outlives its timeout several times over, so a
    /// hung socket can never permanently disable the plugin.
    /// </description></item>
    /// </list>
    /// <para>
    /// <c>HttpWebRequest</c> is used rather than <c>HttpClient</c>: it has been
    /// the more predictable choice on Unity's Mono runtime, and it needs no
    /// extra assembly reference at runtime.
    /// </para>
    /// </remarks>
    public sealed class FocusedClient
    {
        private const int StuckTimeoutFactor = 4;
        private const string ApiPrefix = "/api/v1";

        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
        private readonly HttpTransport _transport;
        private int _busy;
        private long _busySinceTicks;

        public FocusedClient(string baseUrl, string token, int timeoutMs)
        {
            _transport = new HttpTransport(baseUrl, token, timeoutMs);
        }

        public string BaseUrl
        {
            get { return _transport.BaseUrl; }
        }

        public bool Busy
        {
            get { return Interlocked.CompareExchange(ref _busy, 0, 0) != 0; }
        }

        /// <summary>Run queued completions. Call from the Unity main thread.</summary>
        public void Pump(int maxActions)
        {
            ReleaseIfStuck();

            Action action;
            var handled = 0;
            while (handled < maxActions && _pending.TryDequeue(out action))
            {
                handled++;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ChillFocused] response callback failed: " + ex);
                }
            }
        }

        /// <summary>Read the flat session state Focused publishes for its frontends.</summary>
        public void GetFocusStateAsync(Action<FocusStateResponse, string> done)
        {
            Dispatch("GET", ApiPrefix + "/focus", null, done);
        }

        /// <summary>
        /// Tell Focused whether a session is running, and for how long that holds.
        /// </summary>
        /// <remarks>
        /// The lease matters more than the flag: if this plugin dies, is killed, or
        /// stops being scheduled, Focused stops hearing from it, lets the lease
        /// lapse, and resumes everything. A plain boolean would leave applications
        /// suspended with nobody left to say the session ended.
        /// </remarks>
        public void ReportSessionAsync(bool active, bool dryRun, int leaseSeconds,
                                       string source, Action<SimpleResponse, string> done)
        {
            var payload = new SessionStatePayload
            {
                active = active,
                dry_run = dryRun,
                source = source,
                ttl_seconds = leaseSeconds,
            };
            Dispatch("POST", ApiPrefix + "/focus/state", JsonUtility.ToJson(payload), done);
        }

        /// <summary>Ask Focused what is actually running, for the settings panel.</summary>
        public void GetProcessesAsync(int limit, Action<ProcessListResponse, string> done)
        {
            Dispatch("GET", ApiPrefix + "/processes?limit=" + limit.ToString(), null, done);
        }

        public void GetEventsAsync(int since, int limit, Action<EventListResponse, string> done)
        {
            var path = ApiPrefix + "/events?since=" + since.ToString() + "&limit=" + limit.ToString();
            Dispatch("GET", path, null, done);
        }

        /// <summary>
        /// Contribute this plugin's rules. Focused keeps one rule set per source and
        /// merges them, so this can never disturb another plugin's list.
        /// </summary>
        public void PushRulesAsync(RulePayload payload, Action<RulesResponse, string> done)
        {
            Dispatch("PUT", ApiPrefix + "/focus/rules", JsonUtility.ToJson(payload), done);
        }

        /// <summary>Resume everything, immediately. The escape hatch.</summary>
        public void ThawAllAsync(Action<SimpleResponse, string> done)
        {
            Dispatch("POST", ApiPrefix + "/thaw", "{}", done);
        }

        public void ForceScanAsync(Action<ScanResponse, string> done)
        {
            Dispatch("POST", ApiPrefix + "/scan", "{}", done);
        }

        // ------------------------------------------------------------------

        private void Dispatch<T>(string method, string path, string body, Action<T, string> done)
        {
            if (done == null)
            {
                done = delegate { };
            }

            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                // Queued rather than invoked inline so that every callback in
                // this class runs on the main thread, without exception.
                _pending.Enqueue(delegate { done(default(T), "another request is already in flight"); });
                return;
            }

            Interlocked.Exchange(ref _busySinceTicks, DateTime.UtcNow.Ticks);
            ThreadPool.QueueUserWorkItem(delegate
            {
                var parsed = default(T);
                string error = null;
                try
                {
                    var json = _transport.Request(method, path, body);
                    parsed = JsonUtility.FromJson<T>(json);
                }
                catch (Exception ex)
                {
                    // The type matters: PlatformNotSupportedException and
                    // NotSupportedException point at a missing API, WebException
                    // at transport trouble. Logging only the message loses that.
                    error = ex.GetType().Name + ": " + ex.Message;
                }

                // Clear before queueing: a callback is allowed to chain the next
                // request (status -> events).
                Interlocked.Exchange(ref _busy, 0);
                Interlocked.Exchange(ref _busySinceTicks, 0L);
                _pending.Enqueue(delegate { done(parsed, error); });
            });
        }

        private void ReleaseIfStuck()
        {
            if (Interlocked.CompareExchange(ref _busy, 0, 0) == 0)
            {
                return;
            }

            var since = Interlocked.Read(ref _busySinceTicks);
            if (since == 0)
            {
                return;
            }

            var age = new TimeSpan(DateTime.UtcNow.Ticks - since);
            if (age.TotalMilliseconds > (double)_transport.TimeoutMs * StuckTimeoutFactor)
            {
                Interlocked.Exchange(ref _busy, 0);
                Interlocked.Exchange(ref _busySinceTicks, 0L);
                Debug.LogWarning(
                    "[ChillFocused] a request outlived its timeout; clearing the in-flight flag");
            }
        }
    }
}
