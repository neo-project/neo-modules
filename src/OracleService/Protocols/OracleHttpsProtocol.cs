// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Plugins.OracleService is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    class OracleHttpsProtocol : IOracleProtocol
    {
        private readonly HttpClient client = new(new HttpClientHandler() { AllowAutoRedirect = false });

        public OracleHttpsProtocol()
        {
            CustomAttributeData attribute = Assembly.GetExecutingAssembly().CustomAttributes.First(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
            string version = (string)attribute.ConstructorArguments[0].Value;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NeoOracleService", version));
        }

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

            HttpResponseMessage message;
            try
            {
                int redirects = 2;
                do
                {
                    if (!Settings.Default.AllowPrivateHost)
                    {
                        IPHostEntry entry = await Dns.GetHostEntryAsync(uri.Host, cancellation);
                        if (entry.IsInternal())
                            return (OracleResponseCode.Forbidden, null);
                    }
                    message = await client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellation);
                    if (message.Headers.Location is not null)
                    {
                        uri = message.Headers.Location;
                        message = null;
                    }
                } while (message == null && redirects-- > 0);
            }
            catch
            {
                return (OracleResponseCode.Timeout, null);
            }
            if (message is null)
                return (OracleResponseCode.Timeout, null);
            if (message.StatusCode == HttpStatusCode.NotFound)
                return (OracleResponseCode.NotFound, null);
            if (message.StatusCode == HttpStatusCode.Forbidden)
                return (OracleResponseCode.Forbidden, null);
            if (!message.IsSuccessStatusCode)
                return (OracleResponseCode.Error, message.StatusCode.ToString());
            if (!Settings.Default.AllowedContentTypes.Contains(message.Content.Headers.ContentType.MediaType))
                return (OracleResponseCode.ContentTypeNotSupported, null);
            return (OracleResponseCode.Success, await message.Content.ReadAsStringAsync(cancellation));
        }
    }
}
