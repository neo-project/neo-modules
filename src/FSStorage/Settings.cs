using Microsoft.Extensions.Configuration;
using System;

namespace Neo.Plugins
{
    internal class Settings
    {
        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {

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
