using Microsoft.Extensions.Configuration;
using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStoragePlugin
    {
        private string path;
        private bool readCache;

        public override string Description => "Uses LevelDB to store the blockchain data";

        protected override void Configure()
        {
            IConfigurationSection config = GetConfiguration();
            path = string.Format(config.GetSection("Path").Value ?? "Data_LevelDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
            readCache = config.GetSection("ReadCache")?.Get<bool>() ?? false;
        }

        public IStore GetStore()
        {
            return new Store(path, readCache);
        }
    }
}
