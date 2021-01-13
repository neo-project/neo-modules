using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Neo.Plugins.StateService
{
    internal class Settings
    {
        public string Path { get; }
        public bool FullState { get; }
        public bool StartValidate { get; }
        public string[] Validators { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Path = string.Format(section.GetSection("Path").Value ?? "Data_MPT_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
            FullState = GetValueOrDefault(section.GetSection("FullState"), false, p => bool.Parse(p));
            StartValidate = GetValueOrDefault(section.GetSection("StartValidate"), false, p => bool.Parse(p));
            Validators = section.GetSection("Validators").GetChildren().Select(p => p.Get<string>()).ToArray();
        }

        private T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
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
