using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStoragePlugin
    {
        private string path;

        public override void Configure()
        {
            path = string.Format(GetConfiguration().GetSection("Path").Value, ProtocolSettings.Default.Magic.ToString("X8"));
        }

        public IStore GetStore()
        {
            return new Store(path);
        }
    }
}
