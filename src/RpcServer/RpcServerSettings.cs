using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    public class RpcServerSettings
    {
        // These read-only properties are used to in the creation
        // of the WebHost RPC endpoint. These cannot be changed
        // while the server is running

        public IPAddress BindAddress { get; }
        public ushort Port { get; }
        public string SslCert { get; }
        public string SslCertPassword { get; }
        public string[] TrustedAuthorities { get; }
        public int MaxConcurrentConnections { get; }

        // these read-write properties can be changed while
        // the server is running via auto-reconfiguration

        public string RpcUser { get; private set; }
        public string RpcPass { get; private set; }
        public long MaxGasInvoke { get; private set; }
        public long MaxFee { get; private set; }
        public string[] DisabledMethods { get; private set; }

        public RpcServerSettings(IPAddress bindAddress = null,
            ushort port = 10332,
            string sslCert = "",
            string sslCertPassword = "",
            string[] trustedAuthorities = null,
            string rpcUser = "",
            string rpcPass = "",
            decimal maxGasInvoke = 10,
            decimal maxFee = (decimal)0.1,
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
            this.MaxGasInvoke = (long)BigDecimal.Parse(maxGasInvoke.ToString(), NativeContract.GAS.Decimals).Value;
            this.MaxFee = (long)BigDecimal.Parse(maxFee.ToString(), NativeContract.GAS.Decimals).Value;
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

        public void UpdateSettings(RpcServerSettings settings)
        {
            this.RpcUser = settings.RpcUser;
            this.RpcPass = settings.RpcPass;
            this.MaxGasInvoke = settings.MaxGasInvoke;
            this.MaxFee = settings.MaxFee;
            this.DisabledMethods = settings.DisabledMethods;
        }
    }
}
