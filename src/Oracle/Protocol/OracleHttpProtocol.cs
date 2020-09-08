using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JArray = Newtonsoft.Json.Linq.JArray;
using JObject = Newtonsoft.Json.Linq.JObject;

namespace Neo.Plugins
{
    public class OracleHttpProtocol : IOracleProtocol
    {
        public const int Timeout = 5000;
        public bool AllowPrivateHost { get; set; } = false;
        public readonly string[] AllowedFormats = new string[] { "application/json" };

        public byte[] Request(ulong requestId, string url, string filter)
        {
            Utility.Log(nameof(OracleHttpProtocol), LogLevel.Debug, $"Request: {url}");

            Uri.TryCreate(url, UriKind.Absolute, out var uri);
            if (!AllowPrivateHost && IsInternal(Dns.GetHostEntry(uri.Host)))
            {
                // Don't allow private host in order to prevent SSRF

                throw new InvalidOperationException("PolicyError");
            }
            using var handler = new HttpClientHandler
            {
                // TODO: Accept all certificates
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Accept", string.Join(",", AllowedFormats));

            Task<HttpResponseMessage> result = client.GetAsync(uri);

            if (!result.Wait(Timeout)) throw new InvalidOperationException("Timeout");
            if (!result.Result.IsSuccessStatusCode) throw new InvalidOperationException("Response error");
            if (!AllowedFormats.Contains(result.Result.Content.Headers.ContentType.MediaType)) throw new InvalidOperationException("ContentType it's not allowed");

            var taskRet = result.Result.Content.ReadAsStringAsync();
            if (!taskRet.Wait(Timeout)) throw new InvalidOperationException("Timeout");
            var data = Filter(taskRet.Result, filter);
            Console.WriteLine("filter value: " + data);
            return Utility.StrictUTF8.GetBytes(data);
        }

        private string Filter(string input, string filterArgs)
        {
            if (filterArgs is null || filterArgs.Length == 0)
                return input;

            JObject beforeObject = JObject.Parse(input);
            JArray afterObjects = new JArray(beforeObject.SelectTokens(filterArgs).ToArray());
            return afterObjects.ToString();
        }

        internal static bool IsInternal(IPHostEntry entry)
        {
            foreach (var ip in entry.AddressList)
            {
                if (IsInternal(ip)) return true;
            }

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
