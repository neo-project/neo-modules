using System.Threading;
using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;

namespace Neo.FileStorage
{
    public class SideChainSettings
    {
        public bool VerifyImport { get; }
        public StorageSettings Storage { get; }
        public P2PSettings P2P { get; }

        public static SideChainSettings Load(string path, bool optional = true)
        {
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(path, optional: optional).Build();
            return new SideChainSettings(config.GetSection("ApplicationConfiguration"));
        }

        public SideChainSettings(IConfigurationSection section)
        {
            this.VerifyImport = section.GetValue("VerifyImport", true);
            this.Storage = new StorageSettings(section.GetSection("Storage"));
            this.P2P = new P2PSettings(section.GetSection("P2P"));
        }
    }

    public class StorageSettings
    {
        public string Engine { get; }
        public string Path { get; }

        public StorageSettings(IConfigurationSection section)
        {
            this.Engine = section.GetValue("Engine", "LevelDBStore");
            this.Path = section.GetValue("Path", "Data_LevelDB_{0}");
        }
    }

    public class P2PSettings
    {
        public ushort Port { get; }
        public ushort WsPort { get; }
        public int MinDesiredConnections { get; }
        public int MaxConnections { get; }
        public int MaxConnectionsPerAddress { get; }

        public P2PSettings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetValue("Port", "30333"));
            this.WsPort = ushort.Parse(section.GetValue("WsPort", "30334"));
            this.MinDesiredConnections = section.GetValue("MinDesiredConnections", Peer.DefaultMinDesiredConnections);
            this.MaxConnections = section.GetValue("MaxConnections", Peer.DefaultMaxConnections);
            this.MaxConnectionsPerAddress = section.GetValue("MaxConnectionsPerAddress", 3);
        }
    }
}
