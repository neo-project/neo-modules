using Neo.Network.P2P.Payloads;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    class OracleHttpsProtocol : IOracleProtocol
    {
        private readonly HttpClient client = new HttpClient();

        public void Configure()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            foreach (string type in Settings.Default.AllowedContentTypes)
                client.DefaultRequestHeaders.Accept.ParseAdd(type);
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public OracleResponseCode Process(Uri uri, out string response)
        {
            Utility.Log(nameof(OracleHttpsProtocol), LogLevel.Debug, $"Request: {uri.AbsoluteUri}");

            response = null;
            if (!Settings.Default.AllowPrivateHost && Dns.GetHostEntry(uri.Host).IsInternal()) return OracleResponseCode.Forbidden;

            Task<HttpResponseMessage> result = client.GetAsync(uri);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (!result.Wait(Settings.Default.Https.Timeout)) return OracleResponseCode.Timeout;
            if (result.Result.StatusCode == HttpStatusCode.NotFound) return OracleResponseCode.NotFound;
            if (!result.Result.IsSuccessStatusCode) return OracleResponseCode.Error;
            if (!Settings.Default.AllowedContentTypes.Contains(result.Result.Content.Headers.ContentType.MediaType)) return OracleResponseCode.ProtocolNotSupported;
            sw.Stop();
            var taskRet = result.Result.Content.ReadAsStringAsync();
            if (Settings.Default.Https.Timeout <= sw.ElapsedMilliseconds || !taskRet.Wait(Settings.Default.Https.Timeout - (int)sw.ElapsedMilliseconds)) return OracleResponseCode.Timeout;
            response = taskRet.Result;
            return OracleResponseCode.Success;
        }
    }
}
