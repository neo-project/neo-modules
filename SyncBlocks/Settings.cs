using Microsoft.Extensions.Configuration;

namespace Cron.Plugins
{
    internal class Settings
    {
        public static Settings Default { get; private set; }
        
        private Settings(IConfigurationSection section)
        {
            
        }
        
        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}