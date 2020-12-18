using Neo.Network.P2P.Payloads;
using System;
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
            client.Timeout = Settings.Default.Https.Timeout;
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

            Task<HttpResponseMessage> result = client.GetAsync(uri, HttpCompletionOption.ResponseContentRead);
            try
            {
                result.Wait();
            }
            catch (AggregateException)
            {
                return OracleResponseCode.Timeout;
            }
            HttpResponseMessage message = result.Result;
            if (message.StatusCode == HttpStatusCode.NotFound) return OracleResponseCode.NotFound;
            if (message.StatusCode == HttpStatusCode.Forbidden) return OracleResponseCode.Forbidden;
            if (!message.IsSuccessStatusCode) return OracleResponseCode.Error;
            if (!Settings.Default.AllowedContentTypes.Contains(message.Content.Headers.ContentType.MediaType)) return OracleResponseCode.ProtocolNotSupported;
            response = message.Content.ReadAsStringAsync().Result;
            return OracleResponseCode.Success;
        }
    }
}
