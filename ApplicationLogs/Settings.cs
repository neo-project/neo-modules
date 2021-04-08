using Microsoft.Extensions.Configuration;
using Cron.Network.P2P;

namespace Cron.Plugins
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = string.Format(section.GetSection("Path").Value, Message.Magic.ToString("X8"));
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
