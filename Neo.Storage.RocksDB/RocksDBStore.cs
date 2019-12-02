using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class RocksDBStore : Plugin, IStoragePlugin
    {
        private readonly Store _store;

        /// <summary>
        /// Constructor
        /// </summary>
        public RocksDBStore()
        {
            Settings.Load(GetConfiguration());
            _store = new Store(Settings.Default.Path);
        }

        /// <summary>
        /// Configure
        /// </summary>
        public override void Configure()
        {
            // Can't configure the path because NeoSystem store in cache some values

            Settings.Load(GetConfiguration());
        }

        /// <summary>
        /// Get store
        /// </summary>
        /// <returns>RocksDbStore</returns>
        public IStore GetStore() => _store;
    }
}
