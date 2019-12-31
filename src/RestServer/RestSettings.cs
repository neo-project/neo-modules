using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    internal class RestSettings
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

        public static RestSettings Default { get; private set; }

        private RestSettings(IConfigurationSection section)
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
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new RestSettings(section);
        }
    }
}
