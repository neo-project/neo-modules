using Neo.Network.P2P.Payloads;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
        {
            Utility.Log(nameof(OracleHttpsProtocol), LogLevel.Debug, $"Request: {uri.AbsoluteUri}");

            if (!Settings.Default.AllowPrivateHost)
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(uri.Host);
                if (entry.IsInternal())
                    return (OracleResponseCode.Forbidden, null);
            }

            HttpResponseMessage message;
            try
            {
                message = await client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellation);
            }
            catch
            {
                return (OracleResponseCode.Timeout, null);
            }
            if (message.StatusCode == HttpStatusCode.NotFound)
                return (OracleResponseCode.NotFound, null);
            if (message.StatusCode == HttpStatusCode.Forbidden)
                return (OracleResponseCode.Forbidden, null);
            if (!message.IsSuccessStatusCode)
                return (OracleResponseCode.Error, null);
            if (!Settings.Default.AllowedContentTypes.Contains(message.Content.Headers.ContentType.MediaType))
                return (OracleResponseCode.ProtocolNotSupported, null);
            return (OracleResponseCode.Success, await message.Content.ReadAsStringAsync(cancellation));
        }
    }
}
