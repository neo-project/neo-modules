using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class RocksDBStore : Plugin, IStoragePlugin
    {
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
