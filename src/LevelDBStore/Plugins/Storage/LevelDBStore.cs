using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class LevelDBStore : Plugin, IStorageProvider
    {
        private string path;

        public override string Description => "Uses LevelDB to store the blockchain data";

        protected override void Configure()
        {
            path = string.Format(GetConfiguration().GetSection("Path").Value ?? "Data_LevelDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
        }

        public IStore GetStore()
        {
            return new Store(path);
        }
    }
}
