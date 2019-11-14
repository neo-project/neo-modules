using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;

namespace Neo.Plugins.RpcServer
{
    public class RpcServerSettings
    {
        public class SslCert
        {
            /// <summary>
            /// Path
            /// </summary>
            public string Path { get; internal set; }
            /// <summary>
            /// Password
            /// </summary>
            public string Password { get; internal set; }

            /// <summary>
            /// Is valid?
            /// </summary>
            public bool IsValid => !string.IsNullOrEmpty(Path) && !string.IsNullOrEmpty(Password) && File.Exists(Path);
        }

        /// <summary>
        /// Listen end point
        /// </summary>
        public IPEndPoint ListenEndPoint { get; internal set; }

        /// <summary>
        /// SSL config
        /// </summary>
        public SslCert Ssl { get; internal set; }

        public string[] TrustedAuthorities { get; internal set; }

        public IPAddress[] IpBlacklist { get; internal set; }

        public long MaxGasInvoke { get; internal set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">Configuration</param>
        public RpcServerSettings(IConfiguration config = null)
        {
            ListenEndPoint = new IPEndPoint(
                IPAddress.Parse(config.GetValue("BindAddress", "127.0.0.1")),
                ushort.Parse(config.GetValue("Port", "10332")));

            Ssl = new SslCert
            {
                Path = config.GetValue("SslCert", ""),
                Password = config.GetValue("SslCertPassword", "")
            };

            var trustedAuth = config.GetSection("TrustedAuthorities");
            if (trustedAuth.Exists())
            {
                TrustedAuthorities = trustedAuth.Get<List<string>>().ToArray();
            }

            var blacklist = config.GetSection("IpBlacklist");
            if (blacklist.Exists())
            {
                IpBlacklist = blacklist.Get<List<string>>().Select(x => IPAddress.Parse(x)).ToArray();
            }

            MaxGasInvoke = (long)BigDecimal.Parse(config.GetValue("MaxGasInvoke", "10"), NativeContract.GAS.Decimals).Value;
        }
    }
}
