using Microsoft.Extensions.Configuration;
using System;

namespace Neo.Plugins
{
    internal class Settings
    {
        public uint MaxOnImportHeight { get; }

        /// <summary>
        /// Flag for persisting (exporting and importing) transaction state:
        /// fault or halt; number of notifications and stack type.
        /// </summary>
        public bool PersistTXState { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxOnImportHeight = GetValueOrDefault(section.GetSection("MaxOnImportHeight"), 0u, p => uint.Parse(p));
            // TODO - set to false after finishing
            this.PersistTXState = GetValueOrDefault(section.GetSection("PersistTXState"), true, p => bool.Parse(p));
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
