using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;
using System.Reflection;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.Path = string.Format(section.GetSection("Path").Value, Message.Magic.ToString("X8"));
        }
    }
}
