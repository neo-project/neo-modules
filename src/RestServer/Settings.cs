using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    public class Settings
    {
        public IPAddress BindAddress { get; set; }
        public ushort Port { get; set; }
        public string SslCert { get; set; }
        public string SslCertPassword { get; set; }
        public string[] TrustedAuthorities { get; set; }
        public string RpcUser { get; set; }
        public string RpcPass { get; set; }
        public long MaxGasInvoke { get; set; }
        public long MaxFee { get; set; }
        public string[] DisabledMethods { get; set; }
    }
}
