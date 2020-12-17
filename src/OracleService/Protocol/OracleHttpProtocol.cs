using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NUtility = Neo.Utility;

namespace Neo.Plugins
{
    public class OracleHttpProtocol : IOracleProtocol
    {
        public int Timeout { get; set; } = 5000;
        public bool AllowPrivateHost { get; set; } = false;
        public readonly string[] AllowedFormats = new string[] { "application/json" };

        public string Request(string url)
        {
            NUtility.Log(nameof(OracleHttpProtocol), LogLevel.Debug, $"Request: {url}");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) throw new InvalidOperationException("UrlError");
            if (!AllowPrivateHost && IsInternal(Dns.GetHostEntry(uri.Host))) throw new InvalidOperationException("Access to private host is not allowed");

            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Accept", string.Join(",", AllowedFormats));

            Task<HttpResponseMessage> result = client.GetAsync(uri);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (!result.Wait(Timeout)) throw new TimeoutException("Timeout");
            if (result.Result.StatusCode == HttpStatusCode.NotFound) throw new FileNotFoundException($"{url} is not found");
            if (!result.Result.IsSuccessStatusCode) throw new InvalidOperationException("Response error");
            if (!AllowedFormats.Contains(result.Result.Content.Headers.ContentType.MediaType)) throw new InvalidOperationException("ContentType it's not allowed");
            sw.Stop();
            var taskRet = result.Result.Content.ReadAsStringAsync();
            if (Timeout <= sw.ElapsedMilliseconds || !taskRet.Wait(Timeout - (int)sw.ElapsedMilliseconds)) throw new TimeoutException("Timeout");
            return taskRet.Result;
        }

        internal static bool IsInternal(IPHostEntry entry)
        {
            foreach (var ip in entry.AddressList)
                if (IsInternal(ip))
                    return true;

            return false;
        }

        /// <summary>
        ///       ::1          -   IPv6  loopback
        ///       10.0.0.0     -   10.255.255.255  (10/8 prefix)
        ///       127.0.0.0    -   127.255.255.255  (127/8 prefix)
        ///       172.16.0.0   -   172.31.255.255  (172.16/12 prefix)
        ///       192.168.0.0  -   192.168.255.255 (192.168/16 prefix)
        /// </summary>
        /// <param name="ipAddress">Address</param>
        /// <returns>True if it was an internal address</returns>
        internal static bool IsInternal(IPAddress ipAddress)
        {
            if (IPAddress.IsLoopback(ipAddress)) return true;
            if (IPAddress.Broadcast.Equals(ipAddress)) return true;
            if (IPAddress.Any.Equals(ipAddress)) return true;
            if (IPAddress.IPv6Any.Equals(ipAddress)) return true;
            if (IPAddress.IPv6Loopback.Equals(ipAddress)) return true;

            var ip = ipAddress.GetAddressBytes();
            switch (ip[0])
            {
                case 10:
                case 127: return true;
                case 172: return ip[1] >= 16 && ip[1] < 32;
                case 192: return ip[1] == 168;
                default: return false;
            }
        }
    }
}
