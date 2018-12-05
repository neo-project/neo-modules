using Microsoft.Extensions.Configuration;
using System;

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
        public uint HeightToStartRealTimeSyncing { get; }
        /// <summary>
        /// Persisting actions
        /// </summary>
        public PersistActions PersistAction { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            /// Geting settings for storage changes state dumper
            this.BlockCacheSize = GetValueOrDefault(section.GetSection("BlockCacheSize"), 1000u, p => uint.Parse(p));
            this.HeightToBegin = GetValueOrDefault(section.GetSection("HeightToBegin"), 0u, p => uint.Parse(p));
            this.HeightToStartRealTimeSyncing = GetValueOrDefault(section.GetSection("HeightToStartRealTimeSyncing"), 2883000u, p => uint.Parse(p));
            this.PersistAction = GetValueOrDefault(section.GetSection("PersistAction"), PersistActions.StorageChanges, p => (PersistActions)Enum.Parse(typeof(PersistActions), p));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
