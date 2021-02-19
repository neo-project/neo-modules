using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    internal class Settings
    {
        /// <summary>
        /// Amount of storages states (heights) to be dump in a given json file
        /// </summary>
        public uint BlockCacheSize { get; }
        /// <summary>
        /// Height to begin storage dump
        /// </summary>
        public uint HeightToBegin { get; }
        /// <summary>
        /// Height to begin real-time syncing and dumping on, consequently, dumping every block into a single files
        /// </summary>
        public int HeightToStartRealTimeSyncing { get; }
        /// <summary>
        /// Persisting actions
        /// </summary>
        public PersistActions PersistAction { get; }
        public IReadOnlyList<int> Exclude { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            /// Geting settings for storage changes state dumper
            this.BlockCacheSize = section.GetValue("BlockCacheSize", 1000u);
            this.HeightToBegin = section.GetValue("HeightToBegin", 0u);
            this.HeightToStartRealTimeSyncing = section.GetValue("HeightToStartRealTimeSyncing", -1);
            this.PersistAction = section.GetValue("PersistAction", PersistActions.StorageChanges);
            this.Exclude = section.GetSection("Exclude").Exists()
                ? section.GetSection("Exclude").GetChildren().Select(p => int.Parse(p.Value)).ToArray()
                : new[] { NativeContract.Ledger.Id };
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
