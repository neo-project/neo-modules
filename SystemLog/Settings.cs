using Microsoft.Extensions.Configuration;

namespace SystemLog
{
    internal class Settings
    {
        public bool ConsoleOutput { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.ConsoleOutput = section.GetSection("ConsoleOutput").Get<bool>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}