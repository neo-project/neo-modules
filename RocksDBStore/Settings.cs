using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.Storage
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = string.Format(section.GetSection("Path").Value ?? "Data_RocksDB_{0}", ProtocolSettings.Default.Magic.ToString("X8"));
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
