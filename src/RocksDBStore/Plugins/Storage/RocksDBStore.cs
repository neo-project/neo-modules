using Neo.Persistence;

namespace Neo.Plugins.Storage
{
    public class RocksDBStore : Plugin, IStorageProvider
    {
        public override string Description => "Uses RocksDBStore to store the blockchain data";

        /// <summary>
        /// Get store
        /// </summary>
        /// <returns>RocksDbStore</returns>
        public IStore GetStore(string path)
        {
            return new Store(path);
        }
    }
}
