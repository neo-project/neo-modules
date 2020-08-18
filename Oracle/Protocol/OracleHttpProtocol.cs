using Neo;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JArray = Newtonsoft.Json.Linq.JArray;
using JObject = Newtonsoft.Json.Linq.JObject;

namespace Oracle.Protocol
{
    internal class OracleHttpProtocol : IOracleProtocol
    {
        public const int Timeout = 5000;
        public bool AllowPrivateHost { get; internal set; } = false;
        public readonly string[] AllowedFormats = new string[] { "application/json" };

        public byte[] Request(ulong requestId, string url, string filter)
        {
            Utility.Log(nameof(OracleHttpProtocol), LogLevel.Debug, $"Downloading HTTPS request: url={url}");

            Uri.TryCreate(url, UriKind.Absolute, out var uri);
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
            return Encoding.UTF8.GetBytes(data);
        }

        private string Filter(string input, string filterArgs)
        {
            if (filterArgs is null || filterArgs.Length == 0)
                return input;

            JObject beforeObject = JObject.Parse(input);
            JArray afterObjects = new JArray(beforeObject.SelectTokens(filterArgs).ToArray()); // TODO 
            return afterObjects.ToString();
        }
    }
}
