using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    class Settings
    {
        public IReadOnlyList<RpcServerSettings> Servers { get; }

        public Settings(IConfigurationSection section)
        {
            Servers = section.GetSection(nameof(Servers)).GetChildren().Select(p => RpcServerSettings.Load(p)).ToArray();
        }
    }

    public record RpcServerSettings
    {
        public uint Network { get; init; }
        public IPAddress BindAddress { get; init; }
        public ushort Port { get; init; }
        public string SslCert { get; init; }
        public string SslCertPassword { get; init; }
        public string[] TrustedAuthorities { get; init; }
        public int MaxConcurrentConnections { get; init; }
        public string RpcUser { get; init; }
        public string RpcPass { get; init; }
        public long MaxGasInvoke { get; init; }
        public long MaxFee { get; init; }
        public int MaxIteratorResultItems { get; init; }
        public string[] DisabledMethods { get; init; }

        public static RpcServerSettings Default { get; } = new RpcServerSettings
        {
            Network = 5195086u,
            BindAddress = IPAddress.None,
            SslCert = string.Empty,
            SslCertPassword = string.Empty,
            MaxGasInvoke = (long)new BigDecimal(10M, NativeContract.GAS.Decimals).Value,
            MaxFee = (long)new BigDecimal(0.1M, NativeContract.GAS.Decimals).Value,
            TrustedAuthorities = Array.Empty<string>(),
            MaxIteratorResultItems = 100,
            DisabledMethods = Array.Empty<string>(),
            MaxConcurrentConnections = 40,
        };

        public static RpcServerSettings Load(IConfigurationSection section) => new()
        {
            Network = section.GetValue("Network", Default.Network),
            BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value),
            Port = ushort.Parse(section.GetSection("Port").Value),
            SslCert = section.GetSection("SslCert").Value,
            SslCertPassword = section.GetSection("SslCertPassword").Value,
            TrustedAuthorities = section.GetSection("TrustedAuthorities").GetChildren().Select(p => p.Get<string>()).ToArray(),
            RpcUser = section.GetSection("RpcUser").Value,
            RpcPass = section.GetSection("RpcPass").Value,
            MaxGasInvoke = (long)new BigDecimal(section.GetValue<decimal>("MaxGasInvoke", Default.MaxGasInvoke), NativeContract.GAS.Decimals).Value,
            MaxFee = (long)new BigDecimal(section.GetValue<decimal>("MaxFee", Default.MaxFee), NativeContract.GAS.Decimals).Value,
            MaxIteratorResultItems = section.GetValue("MaxIteratorResultItems", Default.MaxIteratorResultItems),
            DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Get<string>()).ToArray(),
            MaxConcurrentConnections = section.GetValue("MaxConcurrentConnections", Default.MaxConcurrentConnections),
        };
    }
}
