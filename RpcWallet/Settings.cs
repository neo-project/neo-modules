using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Neo.Plugins
{
    internal class Settings
    {
        public Fixed8 MaxFee { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxFee = GetValueOrDefault(section.GetSection("MaxFee"), Fixed8.FromDecimal(0.1M), p => Fixed8.Parse(p));
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
