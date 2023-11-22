using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    internal class WebSocketServerSettings
    {
        public uint Network { get; private init; }
        public IPAddress BindAddress { get; private set; }
        public int Port { get; private init; }
        public string SslCert { get; private init; }
        public string SslCertPassword { get; private init; }
        public string[] TrustedAuthorities { get; private init; }
        public uint MessageSize { get; private init; }

        public static WebSocketServerSettings Default => new()
        {
            Network = 5195086u,
            BindAddress = IPAddress.Loopback,
            Port = 10340,
            SslCert = string.Empty,
            SslCertPassword = string.Empty,
            TrustedAuthorities = Array.Empty<string>(),
            MessageSize = 1024 * 8,
        };

        public static WebSocketServerSettings Current { get; private set; }

        public static void Load(IConfigurationSection section)
        {
            Current = new()
            {
                Network = section.GetValue(nameof(Network), Default.Network),
                Port = section.GetValue(nameof(Port), Default.Port),
                SslCert = section.GetSection(nameof(SslCert)).Value,
                SslCertPassword = section.GetSection(nameof(SslCertPassword)).Value,
                TrustedAuthorities = section.GetSection(nameof(TrustedAuthorities)).GetChildren().Select(s => s.Get<string>()).ToArray(),
                MessageSize = section.GetValue(nameof(MessageSize), Default.MessageSize),
            };

            if (IPAddress.TryParse(section.GetSection(nameof(BindAddress)).Value, out var ipAddress))
                Current.BindAddress = ipAddress;
            else
                Current.BindAddress = Default.BindAddress;
        }
    }
}
