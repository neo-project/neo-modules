using Neo.Persistence;
using Neo.Plugins;

namespace Neo.Storage.RocksDB
{
    public class RocksDbPlugin : Plugin, IStoragePlugin
    {
        public readonly RocksDBStore _store;

        public override string Name => nameof(RocksDbPlugin);

        /// <summary>
        /// Constructor
        /// </summary>
        public RocksDbPlugin()
        {
            Settings.Load(GetConfiguration());
            _store = new RocksDBStore(Settings.Default.Path);
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
