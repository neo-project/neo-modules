using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
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
            Servers = section.GetSection(nameof(Servers)).GetChildren().Select(p => new RpcServerSettings(p)).ToArray();
        }
    }

    class RpcServerSettings
    {
        public uint Network { get; }
        public IPAddress BindAddress { get; }
        public ushort Port { get; }
        public string SslCert { get; }
        public string SslCertPassword { get; }
        public string[] TrustedAuthorities { get; }
        public int MaxConcurrentConnections { get; }
        public string RpcUser { get; }
        public string RpcPass { get; }
        public long MaxGasInvoke { get; }
        public long MaxFee { get; }
        public string[] DisabledMethods { get; }

        public RpcServerSettings(IConfigurationSection section)
        {
            this.Network = section.GetValue("Network", 5195086u);
            this.BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value);
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.SslCert = section.GetSection("SslCert").Value;
            this.SslCertPassword = section.GetSection("SslCertPassword").Value;
            this.TrustedAuthorities = section.GetSection("TrustedAuthorities").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.RpcUser = section.GetSection("RpcUser").Value;
            this.RpcPass = section.GetSection("RpcPass").Value;
            this.MaxGasInvoke = (long)new BigDecimal(section.GetValue<decimal>("MaxGasInvoke", 10M), NativeContract.GAS.Decimals).Value;
            this.MaxFee = (long)new BigDecimal(section.GetValue<decimal>("MaxFee", 0.1M), NativeContract.GAS.Decimals).Value;
            this.DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.MaxConcurrentConnections = section.GetValue("MaxConcurrentConnections", 40);
        }
    }
}
