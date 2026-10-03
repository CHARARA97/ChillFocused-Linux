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
    /// Tests for the plugin's actual HTTP implementation, run over real sockets.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpTransport"/> is compiled into this project precisely so the
    /// wire code can be tested without Unity. A stub <see cref="TcpListener"/>
    /// stands in for Focused, which means request formatting, status handling
    /// and error surfacing are all covered here rather than only in-game.
    /// </remarks>
    public class HttpTransportTests
    {
        private sealed class StubServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly string _response;
            private readonly byte[] _rawResponse;
            private readonly Thread _thread;

            public string LastRequest { get; private set; }
            public Exception Failure { get; private set; }

            public StubServer(string response, bool rawBytes = false)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                if (rawBytes)
                {
                    _rawResponse = Encoding.UTF8.GetBytes(response);
                }
                else
                {
                    _response = response;
                }

                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            public int Port
            {
                get { return ((IPEndPoint)_listener.LocalEndpoint).Port; }
            }

            private void Serve()
            {
                try
                {
                    using (var client = _listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 3000;
                        var seen = new StringBuilder();
                        var buffer = new byte[4096];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            seen.Append(Encoding.UTF8.GetString(buffer, 0, read));
                            if (seen.ToString().Contains("\r\n\r\n"))
                            {
                                break;
                            }
                        }

                        LastRequest = seen.ToString();

                        var payload = _rawResponse ?? Encoding.UTF8.GetBytes(_response);
                        stream.Write(payload, 0, payload.Length);
                        stream.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Failure = ex;
                }
                finally
                {
                    try
                    {
                        _listener.Stop();
                    }
                    catch (Exception)
                    {
                        // nothing useful to do while tearing down
                    }
                }
            }

            public void Dispose()
            {
                try
                {
                    _listener.Stop();
                }
                catch (Exception)
                {
                    // already stopped
                }
            }
        }

        private static string Response(int code, string reason, string body)
        {
            var bytes = Encoding.UTF8.GetByteCount(body);
            return "HTTP/1.1 " + code + " " + reason + "\r\n" +
                   "Content-Type: application/json; charset=utf-8\r\n" +
                   "Content-Length: " + bytes + "\r\n" +
                   "Connection: close\r\n\r\n" + body;
        }

        private static HttpTransport Build(StubServer server, string token = "")
        {
            return new HttpTransport("http://127.0.0.1:" + server.Port, token, 3000);
        }

        [Fact]
        public void Returns_the_body_of_a_200()
        {
            using (var server = new StubServer(Response(200, "OK", "{\"ok\": true, \"pid\": 7}")))
            {
                var body = Build(server).Request("GET", "/api/v1/health", null);

                Assert.Contains("\"pid\": 7", body);
            }
        }

        [Fact]
        public void Sends_the_method_path_and_host_header()
        {
            using (var server = new StubServer(Response(200, "OK", "{}")))
            {
                Build(server).Request("GET", "/api/v1/events?since=4&limit=10", null);
                Thread.Sleep(80);

                Assert.Contains("GET /api/v1/events?since=4&limit=10 HTTP/1.1", server.LastRequest);
                Assert.Contains("Host: 127.0.0.1:", server.LastRequest);
                Assert.Contains("Connection: close", server.LastRequest);
            }
        }

        [Fact]
        public void Sends_a_json_body_with_the_right_content_length()
        {
            const string body = "{\"enabled\": true}";

            using (var server = new StubServer(Response(200, "OK", "{\"ok\":true}")))
            {
                Build(server).Request("POST", "/api/v1/enabled", body);
                Thread.Sleep(80);

                Assert.Contains("POST /api/v1/enabled HTTP/1.1", server.LastRequest);
                Assert.Contains("Content-Type: application/json", server.LastRequest);
                Assert.Contains("Content-Length: " + Encoding.UTF8.GetByteCount(body), server.LastRequest);
            }
        }

        [Fact]
        public void Sends_the_token_header_only_when_configured()
        {
            using (var server = new StubServer(Response(200, "OK", "{}")))
            {
                Build(server, "s3cret").Request("GET", "/api/v1/status", null);
                Thread.Sleep(80);

                Assert.Contains("X-ChillFocused-Token: s3cret", server.LastRequest);
            }

            using (var server = new StubServer(Response(200, "OK", "{}")))
            {
                Build(server, string.Empty).Request("GET", "/api/v1/status", null);
                Thread.Sleep(80);

                Assert.DoesNotContain("X-ChillFocused-Token", server.LastRequest);
            }
        }

        [Fact]
        public void Surfaces_the_backends_error_body_on_4xx()
        {
            using (var server = new StubServer(
                Response(400, "Bad Request", "{\"ok\": false, \"error\": \"catch-all pattern\"}")))
            {
                var ex = Assert.Throws<FocusedRequestException>(
                    () => Build(server).Request("POST", "/api/v1/rules", "{}"));

                Assert.Contains("HTTP 400", ex.Message);
                Assert.Contains("catch-all pattern", ex.Message);
            }
        }

        [Fact]
        public void Rejects_a_malformed_response_instead_of_guessing()
        {
            using (var server = new StubServer("not http at all\r\n\r\n", rawBytes: true))
            {
                var ex = Assert.Throws<FocusedRequestException>(
                    () => Build(server).Request("GET", "/api/v1/health", null));

                Assert.Contains("malformed HTTP response", ex.Message);
            }
        }

        [Fact]
        public void Refuses_chunked_encoding_rather_than_mis_parsing_it()
        {
            const string chunked =
                "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n7\r\n{\"a\":1}\r\n0\r\n\r\n";

            using (var server = new StubServer(chunked, rawBytes: true))
            {
                var ex = Assert.Throws<FocusedRequestException>(
                    () => Build(server).Request("GET", "/api/v1/status", null));

                Assert.Contains("chunked", ex.Message);
            }
        }

        [Fact]
        public void Preserves_a_body_that_contains_blank_lines()
        {
            const string body = "{\"detail\":\"line one\\n\\nline two\"}";

            using (var server = new StubServer(Response(200, "OK", body)))
            {
                var received = Build(server).Request("GET", "/api/v1/status", null);

                Assert.Equal(body, received);
            }
        }

        [Fact]
        public void Reports_a_refused_connection_as_a_request_failure()
        {
            // Bind and immediately release a port so nothing is listening on it.
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
            }

            var transport = new HttpTransport("http://127.0.0.1:" + port, string.Empty, 1000);

            var ex = Assert.Throws<FocusedRequestException>(
                () => transport.Request("GET", "/api/v1/status", null));

            Assert.Contains("SocketException", ex.Message);
        }

        [Fact]
        public void Defaults_to_the_loopback_backend_when_the_url_is_empty()
        {
            Assert.Equal("http://127.0.0.1:8766", new HttpTransport(null, null, 0).BaseUrl);
            Assert.Equal("http://127.0.0.1:8766", new HttpTransport("   ", "", 10).BaseUrl);
        }

        [Fact]
        public void Trailing_slashes_do_not_produce_double_slashes()
        {
            Assert.Equal("http://127.0.0.1:9000", new HttpTransport("http://127.0.0.1:9000/", "", 500).BaseUrl);
        }

        [Fact]
        public void Enforces_a_minimum_timeout()
        {
            Assert.Equal(100, new HttpTransport("http://127.0.0.1:1", "", 1).TimeoutMs);
            Assert.Equal(2500, new HttpTransport("http://127.0.0.1:1", "", 2500).TimeoutMs);
        }

        [Fact]
        public void Extract_body_handles_a_missing_separator_gracefully()
        {
            Assert.Throws<FocusedRequestException>(
                () => HttpTransport.ExtractBody(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK")));
        }

        [Fact]
        public void Extract_body_accepts_an_empty_success_body()
        {
            Assert.Equal(string.Empty, HttpTransport.ExtractBody(
                Encoding.UTF8.GetBytes("HTTP/1.1 204 No Content\r\n\r\n")));
        }
    }
}
