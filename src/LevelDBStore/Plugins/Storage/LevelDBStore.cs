using Microsoft.Extensions.Configuration;
using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStoragePlugin
    {
        private string path;
        private bool readCache;

        protected override void Configure()
        {
            IConfigurationSection config = GetConfiguration();
            path = string.Format(config.GetSection("Path").Value ?? "Data_LevelDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
            bool.TryParse(config.GetSection("ReadCache")?.Value, out readCache);
        }

        public IStore GetStore()
        {
            return new Store(path, readCache);
        }
    }
}
