using Microsoft.Extensions.Configuration;
using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStoragePlugin
    {
        private string path;
        private int bloomFilterBitsPerKey;

        protected override void Configure()
        {
            IConfigurationSection config = GetConfiguration();
            path = string.Format(config.GetSection("Path").Value ?? "Data_LevelDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
            bloomFilterBitsPerKey = int.Parse(config.GetSection("BloomFilterBitsPerKey").Value ?? "20");
        }

        public IStore GetStore()
        {
            return new Store(path);
        }
    }
}
