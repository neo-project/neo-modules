using Microsoft.Extensions.Configuration;
using System;
using System.Net;

namespace Neo.Plugins
{
    public class WebSocketServerSettings
    {
        public uint Network { get; private init; }
        public IPAddress BindAddress { get; private set; }
        public int Port { get; private init; }
        public string SslCertFile { get; private init; }
        public string SslCertPassword { get; private init; }
        public string[] TrustedAuthorities { get; private init; }
        public bool EnableBasicAuthentication { get; private init; }
        public string User { get; private init; }
        public string Pass { get; private init; }
        public string[] AllowOrigins { get; private init; }
        public uint ConcurrentProxyTimeout { get; private init; }
        public uint MessageSize { get; private init; }
        public long MaxGasInvoke { get; private init; }
        public bool DebugMode { get; private init; }

        public static WebSocketServerSettings Default => new()
        {
            Network = 5195086u,
            BindAddress = IPAddress.Loopback,
            Port = 10340,
            SslCertFile = string.Empty,
            SslCertPassword = string.Empty,
            TrustedAuthorities = Array.Empty<string>(),
            EnableBasicAuthentication = false,
            User = string.Empty,
            Pass = string.Empty,
            AllowOrigins = Array.Empty<string>(),
            ConcurrentProxyTimeout = 120,
            MessageSize = 1024 * 4,
            MaxGasInvoke = 20_00000000L,
            DebugMode = false,
        };

        public static WebSocketServerSettings Current { get; private set; }

        internal static void Load(IConfigurationSection section)
        {
            Current = new()
            {
                Network = section.GetValue(nameof(Network), Default.Network),
                Port = section.GetValue(nameof(Port), Default.Port),
                SslCertFile = section.GetValue(nameof(SslCertFile), Default.SslCertFile),
                SslCertPassword = section.GetValue(nameof(SslCertPassword), Default.SslCertPassword),
                TrustedAuthorities = section.GetSection(nameof(TrustedAuthorities))?.Get<string[]>() ?? Default.TrustedAuthorities,
                EnableBasicAuthentication = section.GetValue(nameof(EnableBasicAuthentication), Default.EnableBasicAuthentication),
                User = section.GetValue(nameof(User), Default.User),
                Pass = section.GetValue(nameof(Pass), Default.Pass),
                AllowOrigins = section.GetSection(nameof(AllowOrigins))?.Get<string[]>() ?? Default.AllowOrigins,
                ConcurrentProxyTimeout = section.GetValue(nameof(ConcurrentProxyTimeout), Default.ConcurrentProxyTimeout),
                MessageSize = section.GetValue(nameof(MessageSize), Default.MessageSize),
                MaxGasInvoke = section.GetValue(nameof(MaxGasInvoke), Default.MaxGasInvoke),
                DebugMode = section.GetValue(nameof(DebugMode), Default.DebugMode),
            };

            if (IPAddress.TryParse(section.GetSection(nameof(BindAddress)).Value, out var ipAddress))
                Current.BindAddress = ipAddress;
            else
                Current.BindAddress = Default.BindAddress;
        }
    }
}
