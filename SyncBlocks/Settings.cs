using Microsoft.Extensions.Configuration;

namespace Cron.Plugins
{
    internal class Settings
    {
        public string ImportDirectory { get; set; }

        public string ExportDirectory { get; set; }
        
        public static Settings Default { get; private set; }
        
        private Settings(IConfigurationSection section)
        {
            ImportDirectory = GetValueOrDefault(section.GetSection("ImportDirectory"), string.Empty);
            ExportDirectory = GetValueOrDefault(section.GetSection("ExportDirectory"), string.Empty);
        }

        private static string GetValueOrDefault(IConfigurationSection section, string defaultValue)
        {
            return section.Value ?? defaultValue;
        }
        
        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}