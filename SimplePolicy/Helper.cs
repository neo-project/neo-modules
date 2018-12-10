using Microsoft.Extensions.Configuration;
using System;

namespace Neo.Plugins
{
    internal static class Helper
    {
        public static T GetValueOrDefault<T>(this IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}
