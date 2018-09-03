﻿using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;

namespace Neo.Plugins
{
    internal class Settings
    {
        public uint MaxOnImportHeight { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.MaxOnImportHeight = GetValueOrDefault(section.GetSection("MaxOnImportHeight"), 0u, p => uint.Parse(p));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}
