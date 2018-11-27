using Microsoft.Extensions.Configuration;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
        /// Auxiliar variable that caches all block storage changes for every BlockCacheSize cached blocks
        /// </summary>
        public string BlockStorageCache;
        /// <summary>
        /// Possible Persisting actions:
        /// 0: Calls Storage Changes Dumping
        /// 1: Other persisting actions
        /// </summary>
        public uint PersistAction { get; }


        public static Settings Default { get; }


        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
	        /// Geting settings for storage changes state dumper
            this.BlockCacheSize = GetValueOrDefault(section.GetSection("BlockCacheSize"), 1000u, p => uint.Parse(p));
            this.HeightToBegin = GetValueOrDefault(section.GetSection("HeightToBegin"), 0u, p => uint.Parse(p));
            this.HeightToStartRealTimeSyncing = GetValueOrDefault(section.GetSection("HeightToStartRealTimeSyncing"), 2883000u, p => uint.Parse(p));
            this.PersistAction = GetValueOrDefault(section.GetSection("PersistAction"), 0u, p => uint.Parse(p));
            this.BlockStorageCache = "[";
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }

}
