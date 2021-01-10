using Microsoft.Extensions.Configuration;

namespace Neo.Consensus
{
    class Settings
    {
        public bool IgnoreRecoveryLogs { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            IgnoreRecoveryLogs = section.GetValue("IgnoreRecoveryLogs", false);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
