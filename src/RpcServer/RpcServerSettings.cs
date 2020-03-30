using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    public class RpcServerSettings
    {
        public IPAddress BindAddress { get; }
        public ushort Port { get; }
        public string SslCert { get; }
        public string SslCertPassword { get; }
        public string[] TrustedAuthorities { get; }
        public string RpcUser { get; }
        public string RpcPass { get; }
        public long MaxGasInvoke { get; }
        public long MaxFee { get; }
        public string[] DisabledMethods { get; }
        public int MaxConcurrentConnections { get; }

        public RpcServerSettings(IPAddress bindAddress = null,
            ushort port = 10332,
            string sslCert = "",
            string sslCertPassword = "",
            string[] trustedAuthorities = null,
            string rpcUser = "",
            string rpcPass = "",
            string masGasInvoke = "10",
            string maxFee = "0.1",
            string[] disabledMethods = null,
            int maxConcurrentConnections = 40)
        {
            this.BindAddress = bindAddress ?? IPAddress.Loopback;
            this.Port = port;
            this.SslCert = sslCert;
            this.SslCertPassword = sslCertPassword;
            this.TrustedAuthorities = trustedAuthorities ?? Array.Empty<string>();
            this.RpcUser = rpcUser;
            this.RpcPass = rpcPass;
            this.MaxGasInvoke = (long)BigDecimal.Parse(masGasInvoke, NativeContract.GAS.Decimals).Value;
            this.MaxFee = (long)BigDecimal.Parse(maxFee, NativeContract.GAS.Decimals).Value;
            this.DisabledMethods = disabledMethods ?? Array.Empty<string>();
            this.MaxConcurrentConnections = maxConcurrentConnections;
        }

        public RpcServerSettings(IConfigurationSection section)
        {
            this.BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value);
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.SslCert = section.GetSection("SslCert").Value;
            this.SslCertPassword = section.GetSection("SslCertPassword").Value;
            this.TrustedAuthorities = section.GetSection("TrustedAuthorities").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.RpcUser = section.GetSection("RpcUser").Value;
            this.RpcPass = section.GetSection("RpcPass").Value;
            this.MaxGasInvoke = (long)BigDecimal.Parse(section.GetValue("MaxGasInvoke", "10"), NativeContract.GAS.Decimals).Value;
            this.MaxFee = (long)BigDecimal.Parse(section.GetValue("MaxFee", "0.1"), NativeContract.GAS.Decimals).Value;
            this.DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.MaxConcurrentConnections = section.GetValue("MaxConcurrentConnections", 40);
        }
    }
}
