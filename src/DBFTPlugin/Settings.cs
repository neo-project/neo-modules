using Microsoft.Extensions.Configuration;

namespace Neo.Consensus
{
    class Settings
    {
        public string RecoveryLogs { get; }
        public bool IgnoreRecoveryLogs { get; }
        public bool AutoStart { get; }
        public uint Active { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            RecoveryLogs = section.GetValue("RecoveryLogs", "ConsensusState");
            IgnoreRecoveryLogs = section.GetValue("IgnoreRecoveryLogs", false);
            AutoStart = section.GetValue("AutoStart", false);
            Active = section.GetValue("Active", 5195086u);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
