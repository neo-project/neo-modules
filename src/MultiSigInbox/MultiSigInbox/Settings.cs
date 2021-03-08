using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.MultiSigInbox
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; private set; }
        public bool AutoStart { get; internal set; }

        private Settings(IConfigurationSection section)
        {
            Path = string.Format(section.GetValue("Path", "Data_MultiSigInbox_{0}"), ProtocolSettings.Default.Magic.ToString("X8"));
            AutoStart = section.GetValue("AutoStart", false);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
