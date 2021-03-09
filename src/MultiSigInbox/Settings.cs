using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.MultiSigInbox
{
    internal class Settings
    {
        public string Path { get; }
        public bool AutoStart { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Path = section.GetValue("Path", "Data_MultiSigInbox_{0}");
            AutoStart = section.GetValue("AutoStart", false);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
