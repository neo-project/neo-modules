using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string RpcUser { get; }
        public string RpcPass { get; }
        public string[] DisabledMethods { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.RpcUser = section.GetSection("RpcUser").Value;
            this.RpcPass = section.GetSection("RpcPass").Value;
            this.DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Value).ToArray();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
