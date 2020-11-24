using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Neo.Plugins.MPTService
{
    internal class Settings
    {
        public string Path { get; }
        public bool FullState { get; }
        public bool StartValidate { get; }
        public string Wallet { get; }
        public string Password { get; }
        public string[] Validators { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = string.Format(section.GetSection("Path").Value ?? "Data_MPT_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
            this.FullState = GetValueOrDefault(section.GetSection("FullState"), false, p => bool.Parse(p));
            this.Validators = section.GetSection("Validators").GetChildren().Select(p => p.Get<string>()).ToArray();

            var validator_section = section.GetSection("ValidatorConfiguration");
            if (validator_section.Exists())
            {
                this.StartValidate = GetValueOrDefault(validator_section.GetSection("StartValidate"), false, p => bool.Parse(p));
                this.Wallet = string.Format(validator_section.GetSection("Wallet").Value ?? "", ProtocolSettings.Default.Magic.ToString("X8"));
                this.Password = validator_section.GetSection("Password").Value;
            }
            else
            {
                this.StartValidate = false;
            }
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
