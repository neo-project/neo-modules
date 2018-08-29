using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Reflection;

namespace Neo.Plugins
{
    internal class Settings
    {
        public string[] DisabledMethods { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Value).ToArray();
        }
    }
}
