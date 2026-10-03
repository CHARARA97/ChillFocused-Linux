using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ChillFocus.Core;

namespace NetProbe
{
    /// <summary>
    /// Compares the two HTTP implementations from inside a Wine/Proton prefix.
    /// </summary>
    /// <remarks>
    /// When the BepInEx plugin cannot reach the daemon, the interesting question
    /// is whether the network is at fault or the client is. This program answers
    /// both halves and, crucially, runs the plugin's <b>real</b> transport
    /// (<c>HttpTransport.cs</c> is compiled straight into this executable) next
    /// to the legacy <c>HttpWebRequest</c> path:
    ///
    /// <list type="bullet">
    /// <item><description>raw TCP connect fails -> the network is the problem;</description></item>
    /// <item><description>TCP works but <c>HttpWebRequest</c> throws -> the Mono HTTP stack is the problem;</description></item>
    /// <item><description>both work -> the failure is elsewhere in the plugin.</description></item>
    /// </list>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var baseUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:8765";
            var uri = new Uri(baseUrl);

            Console.WriteLine("netprobe: " + baseUrl);
            Console.WriteLine("  runtime        : " + Environment.Version + " / " + Environment.OSVersion);

            var ok = true;
            ok &= CheckTcp(uri);
            ok &= CheckHttpWebRequest(baseUrl);
            ok &= CheckTransport(baseUrl);

            Console.WriteLine();
            Console.WriteLine(ok
                ? "  RESULT         : the prefix can reach the host daemon"
                : "  RESULT         : at least one check failed");
            return ok ? 0 : 1;
        }

        private static bool CheckTcp(Uri uri)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connect = client.BeginConnect(uri.Host, uri.Port, null, null);
                    if (!connect.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                    {
                        Console.WriteLine("  tcp connect    : TIMEOUT");
                        return false;
                    }

                    client.EndConnect(connect);
                    Console.WriteLine("  tcp connect    : OK -> " + client.Client.RemoteEndPoint);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  tcp connect    : FAILED " + Describe(ex));
                return false;
            }
        }

        /// <summary>The implementation the plugin originally used.</summary>
        private static bool CheckHttpWebRequest(string baseUrl)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/api/v1/health");
                request.Method = "GET";
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.Proxy = null;
                request.Accept = "application/json";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var body = reader.ReadToEnd().Trim();
                    Console.WriteLine("  HttpWebRequest : OK " + (int)response.StatusCode + " " + Clip(body, 80));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  HttpWebRequest : FAILED " + Describe(ex));
                return false;
            }
        }

        /// <summary>The implementation the plugin uses now.</summary>
        private static bool CheckTransport(string baseUrl)
        {
            try
            {
                var transport = new HttpTransport(baseUrl, string.Empty, 4000);
                var health = transport.Request("GET", "/api/v1/health", null);
                Console.WriteLine("  HttpTransport  : GET /health -> " + Clip(health.Trim(), 80));

                var status = transport.Request("GET", "/api/v1/status", null);
                Console.WriteLine("  HttpTransport  : GET /status -> " + Clip(status.Trim(), 80));

                // Exercise the POST path too, so request bodies and the 4xx path
                // are covered by the probe as well as by the unit tests.
                var enabled = transport.Request("POST", "/api/v1/enabled", "{\"enabled\": false}");
                Console.WriteLine("  HttpTransport  : POST /enabled -> " + Clip(enabled.Trim(), 80));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  HttpTransport  : FAILED " + Describe(ex));
                return false;
            }
        }

        private static string Describe(Exception ex)
        {
            var message = ex.GetType().Name + ": " + ex.Message;
            if (ex.InnerException != null)
            {
                message += "  [inner " + ex.InnerException.GetType().Name + ": " +
                           ex.InnerException.Message + "]";
            }

            return Clip(message, 200);
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var flat = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return flat.Length <= max ? flat : flat.Substring(0, max) + "...";
        }
    }
}
