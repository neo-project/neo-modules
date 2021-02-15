using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string Path { get; }
        public uint Network { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = section.GetValue("Path", "ApplicationLogs_{0}");
            this.Network = section.GetValue("Network", 5195086u);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
