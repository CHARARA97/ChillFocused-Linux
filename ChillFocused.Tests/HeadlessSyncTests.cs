using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// End-to-end cycle tests for the sync thread, over a real loopback socket.
    /// The regression they protect is PLG-02: a rejected rule push must not stop
    /// the heartbeat.
    /// </summary>
    public class HeadlessSyncTests
    {
        private sealed class SequenceServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Thread _thread;
            private readonly int _expected;
            private readonly int _rulesStatus;
            private readonly List<string> _paths = new List<string>();

            public SequenceServer(int expected, int rulesStatus)
            {
                _expected = expected;
                _rulesStatus = rulesStatus;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            public int Port
            {
                get { return ((IPEndPoint)_listener.LocalEndpoint).Port; }
            }

            public string[] Paths
            {
                get { lock (_paths) { return _paths.ToArray(); } }
            }

            private void Serve()
            {
                try
                {
                    for (var i = 0; i < _expected; i++)
                    {
                        using (var client = _listener.AcceptTcpClient())
                        using (var stream = client.GetStream())
                        {
                            stream.ReadTimeout = 3000;
                            var head = new StringBuilder();
                            var buffer = new byte[4096];
                            while (!head.ToString().Contains("\r\n\r\n"))
                            {
                                var read = stream.Read(buffer, 0, buffer.Length);
                                if (read <= 0)
                                {
                                    break;
                                }

                                head.Append(Encoding.ASCII.GetString(buffer, 0, read));
                            }

                            var first = head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                            var parts = first.Split(' ');
                            var path = parts.Length >= 2 ? parts[1] : string.Empty;
                            lock (_paths) { _paths.Add(path); }

                            var body = "{\"ok\":true,\"pid\":42,\"active\":true}";
                            var status = 200;
                            if (path.Contains("/rules") && _rulesStatus != 200)
                            {
                                status = _rulesStatus;
                                body = "{\"ok\":false,\"error\":\"bad rule\"}";
                            }

                            var reason = status == 200 ? "OK" : "Bad Request";
                            var payload = "HTTP/1.1 " + status + " " + reason + "\r\n" +
                                          "Content-Type: application/json\r\n" +
                                          "Content-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n" +
                                          "Connection: close\r\n\r\n" + body;
                            var bytes = Encoding.UTF8.GetBytes(payload);
                            stream.Write(bytes, 0, bytes.Length);
                            stream.Flush();
                        }
                    }
                }
                catch (Exception)
                {
                    // The test asserts the observed paths; a teardown error is not useful.
                }
            }

            public void Dispose()
            {
                try { _listener.Stop(); } catch (Exception) { }
                try { _thread.Join(1000); } catch (Exception) { }
            }
        }

        private static HeadlessRunner Runner(SequenceServer server)
        {
            var transport = new HttpTransport(
                "http://127.0.0.1:" + server.Port, string.Empty, 3000);
            var runner = new HeadlessRunner(
                () => new FocusSettings
                {
                    ProcessNames = new[] { "firefox" },
                    PollIntervalSeconds = 0.2f,
                    SessionLeaseSeconds = 30,
                },
                transport,
                new FocusStatusProbe(() => transport.Request("GET", "/api/v1/focus", null)),
                new RulePublisher(
                    body => transport.Request("PUT", "/api/v1/focus/rules", body),
                    _ => { }, _ => { }),
                new SessionReporter(
                    body => transport.Request("POST", "/api/v1/focus/state", body)),
                0.5,
                _ => { },
                _ => { });
            runner.GameStateProvider = () => new TimerState
            {
                Known = true,
                Mode = TimerMode.Pomodoro,
                Working = true,
                Running = true,
                Phase = "Work",
                ReadAt = GameClock.Now,
                ChangedAt = GameClock.Now,
            };
            return runner;
        }

        [Fact]
        public void A_rejected_rule_push_does_not_stop_the_heartbeat()
        {
            using (var server = new SequenceServer(expected: 3, rulesStatus: 400))
            {
                var runner = Runner(server);
                runner.Cycle();

                Assert.Contains("/api/v1/focus", server.Paths);
                Assert.Contains("/api/v1/focus/rules", server.Paths);
                Assert.Contains("/api/v1/focus/state", server.Paths);
                Assert.Equal(0, runner.PushCount);
            }
        }

        [Fact]
        public void A_successful_cycle_sends_rules_then_state()
        {
            using (var server = new SequenceServer(expected: 3, rulesStatus: 200))
            {
                var runner = Runner(server);
                runner.Cycle();

                Assert.Equal(1, runner.PushCount);
                Assert.Contains("/api/v1/focus/state", server.Paths);
            }
        }
    }
}
