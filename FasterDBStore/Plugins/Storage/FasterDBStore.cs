using Neo.Persistence;

namespace Neo.Plugins.Storage.Plugins.Storage
{
    public class FasterDBStore : Plugin, IStoragePlugin
    {
        /// <summary>
        /// Configure
        /// </summary>
        public override void Configure()
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
