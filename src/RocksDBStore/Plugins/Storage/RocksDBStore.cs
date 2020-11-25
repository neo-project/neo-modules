using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class RocksDBStore : Plugin, IStorageProvider
    {
        public override string Description => "Uses RocksDBStore to store the blockchain data";

        /// <summary>
        /// Configure
        /// </summary>
        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        /// <summary>
        /// Get store
        /// </summary>
        /// <returns>RocksDbStore</returns>
        public IStore GetStore() => new Store(Settings.Default.Path);
    }
}
