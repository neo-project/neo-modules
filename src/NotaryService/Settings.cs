using Microsoft.Extensions.Configuration;

namespace Neo.Plugins
{
    public class Settings
    {
        public int Capacity { get; }
        public uint Network { get; }
        public bool AutoStart { get; }

        public static Settings Default { get; private set; }

        public Settings(IConfigurationSection section)
        {
            Capacity = section.GetValue("Capacity", 1000);
            Network = section.GetValue("Network", 5195086u);
            AutoStart = section.GetValue("AutoStart", false);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
