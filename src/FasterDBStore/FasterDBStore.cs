using Neo.Persistence;

namespace Neo.Plugins.Storage
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
        /// <returns>FasterDbStore</returns>
        public IStore GetStore() => new Store(Settings.Default.Path);
    }
}
