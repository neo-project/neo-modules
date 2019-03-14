using Microsoft.Extensions.Configuration;
using System;


namespace Neo.Plugins
{
    internal class Settings
    {
        public Fixed8 MaxGasInvoke { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxGasInvoke = GetValueOrDefault(section.GetSection("MaxGasInvoke"), Fixed8.FromDecimal(0), p => Fixed8.Parse(p));
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
