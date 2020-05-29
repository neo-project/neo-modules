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
            int.TryParse(config.GetSection("BloomFilterBitsPerKey")?.Value, out bloomFilterBitsPerKey);
        }

        public IStore GetStore()
        {
            return new Store(path, bloomFilterBitsPerKey);
        }
    }
}
