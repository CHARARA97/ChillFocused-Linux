using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ChillFocused.Core
{
    /// <summary>
    /// A minimal HTTP/1.1 client over a plain socket.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This file is deliberately free of any UnityEngine reference, so it is
    /// compiled into both the plugin and the test project. That is what makes
    /// the wire code testable: the same source that runs inside the game can be
    /// exercised against a stub listener on the host, and against the real
    /// Focused from inside the Proton prefix via
    /// <c>scripts/probe-wine-network.sh</c>.
    /// </para>
    /// <para>
    /// <c>HttpWebRequest</c> is avoided on purpose. A plugin built against net472
    /// resolves <c>System.dll</c> to whatever Unity's Mono profile supplies, and
    /// if that stack declines the call the failure is an exception buried inside
    /// the background worker. <c>TcpClient</c> is a much smaller dependency and
    /// has been verified to work from inside this game's Proton prefix.
    /// </para>
    /// <para>
    /// The Focused always answers with an explicit <c>Content-Length</c> and
    /// honours <c>Connection: close</c>, so reading to EOF yields exactly one
    /// complete response and chunked transfer encoding never appears. Responses
    /// that used chunking would be rejected rather than silently mis-parsed.
    /// </para>
    /// </remarks>
    public sealed class HttpTransport
    {
        private readonly string _baseUrl;
        private readonly string _token;
        private readonly int _timeoutMs;

        public HttpTransport(string baseUrl, string token, int timeoutMs)
        {
            var url = (baseUrl ?? string.Empty).Trim();
            if (url.Length == 0)
            {
                url = "http://127.0.0.1:8766";
            }

            _baseUrl = url.TrimEnd('/');
            _token = token ?? string.Empty;
            _timeoutMs = timeoutMs < 100 ? 100 : timeoutMs;
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
        }

        public int TimeoutMs
        {
            get { return _timeoutMs; }
        }

        /// <summary>
        /// Perform one request. Returns the response body, or throws
        /// <see cref="FocusedRequestException"/> with a message worth logging.
        /// </summary>
        public string Request(string method, string path, string body)
        {
            try
            {
                return Send(method, path, body);
            }
            catch (FocusedRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One exception type for every failure mode, so callers (and the
                // log) never have to distinguish SocketException from
                // UriFormatException from IOException.
                throw new FocusedRequestException(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private string Send(string method, string path, string body)
        {
            var uri = new Uri(_baseUrl + path);
            var host = uri.Host;
            var port = uri.IsDefaultPort ? 80 : uri.Port;
            var payload = body != null ? Encoding.UTF8.GetBytes(body) : new byte[0];

            using (var client = new TcpClient())
            {
                var connect = client.BeginConnect(host, port, null, null);
                if (!connect.AsyncWaitHandle.WaitOne(_timeoutMs))
                {
                    throw new FocusedRequestException(
                        "connect to " + host + ":" + port + " timed out after " + _timeoutMs + "ms");
                }

                client.EndConnect(connect);
                client.ReceiveTimeout = _timeoutMs;
                client.SendTimeout = _timeoutMs;

                using (var stream = client.GetStream())
                {
                    var head = new StringBuilder();
                    head.Append(method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");
                    head.Append("Host: ").Append(host).Append(':').Append(port).Append("\r\n");
                    head.Append("Accept: application/json\r\n");
                    head.Append("Connection: close\r\n");
                    if (_token.Length > 0)
                    {
                        head.Append("X-ChillFocused-Token: ").Append(_token).Append("\r\n");
                    }

                    if (payload.Length > 0)
                    {
                        head.Append("Content-Type: application/json\r\n");
                        head.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
                    }

                    head.Append("\r\n");

                    var headBytes = Encoding.ASCII.GetBytes(head.ToString());
                    stream.Write(headBytes, 0, headBytes.Length);
                    if (payload.Length > 0)
                    {
                        stream.Write(payload, 0, payload.Length);
                    }

                    stream.Flush();
                    return ExtractBody(ReadToEnd(stream));
                }
            }
        }

        internal static byte[] ReadToEnd(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                int read;
                // Connection: close means EOF is a complete response.
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }

        internal static string ExtractBody(byte[] raw)
        {
            var text = Encoding.UTF8.GetString(raw);
            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0)
            {
                // Every well-formed HTTP response has a header terminator. Its
                // absence means the response was truncated (connection dropped
                // mid-headers), which must be reported rather than turned into a
                // silently empty body.
                throw new FocusedRequestException(
                    "truncated HTTP response: " + Clip(text, 120));
            }

            var head = text.Substring(0, split);
            var body = text.Substring(split + 4);

            var statusLine = head.Split('\n')[0].Trim();
            var parts = statusLine.Split(' ');
            var code = 0;
            if (parts.Length >= 2)
            {
                int.TryParse(parts[1], out code);
            }

            if (code == 0)
            {
                throw new FocusedRequestException("malformed HTTP response: " + Clip(statusLine, 120));
            }

            if (LooksChunked(head))
            {
                throw new FocusedRequestException(
                    "Focused used chunked transfer encoding, which this client does not implement");
            }

            if (code >= 400)
            {
                // Surface Focused's own JSON error instead of just the code.
                throw new FocusedRequestException(
                    "HTTP " + code + " from Focused: " + Clip(body, 300));
            }

            return body;
        }

        private static bool LooksChunked(string head)
        {
            return head.IndexOf("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase) >= 0
                || head.IndexOf("Transfer-Encoding:chunked", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Clip(string value, int max)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var flat = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return flat.Length <= max ? flat : flat.Substring(0, max) + "...";
        }
    }

    /// <summary>A transport or protocol failure, with a message worth logging.</summary>
    public sealed class FocusedRequestException : Exception
    {
        public FocusedRequestException(string message)
            : base(message)
        {
        }
    }
}
