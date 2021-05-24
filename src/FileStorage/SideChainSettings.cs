using System.Threading;
using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;

namespace Neo.FileStorage
{
    public class SideChainSettings
    {
        public bool VerifyImport { get; }
        public LoggerSettings Logger { get; }
        public StorageSettings Storage { get; }
        public P2PSettings P2P { get; }
        public UnlockWalletSettings UnlockWallet { get; }

        public static SideChainSettings Load(string path, bool optional = true)
        {
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(path, optional: optional).Build();
            return new SideChainSettings(config.GetSection("ApplicationConfiguration"));
        }

        public SideChainSettings(IConfigurationSection section)
        {
            this.VerifyImport = section.GetValue("VerifyImport", true);
            this.Logger = new LoggerSettings(section.GetSection("Logger"));
            this.Storage = new StorageSettings(section.GetSection("Storage"));
            this.P2P = new P2PSettings(section.GetSection("P2P"));
            this.UnlockWallet = new UnlockWalletSettings(section.GetSection("UnlockWallet"));
        }
    }

    public class LoggerSettings
    {
        public string Path { get; }
        public bool ConsoleOutput { get; }
        public bool Active { get; }

        public LoggerSettings(IConfigurationSection section)
        {
            this.Path = section.GetValue("Path", "Logs_{0}");
            this.ConsoleOutput = section.GetValue("ConsoleOutput", false);
            this.Active = section.GetValue("Active", false);
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

    public class UnlockWalletSettings
    {
        public string Path { get; }
        public string Password { get; }
        public bool IsActive { get; }

        public UnlockWalletSettings(IConfigurationSection section)
        {
            if (section.Exists())
            {
                this.Path = section.GetValue("Path", "");
                this.Password = section.GetValue("Password", "");
                this.IsActive = bool.Parse(section.GetValue("IsActive", "false"));
            }
        }
    }
}
