using Microsoft.Extensions.Configuration;

namespace Neo.Consensus
{
    public class Settings
    {
        public string RecoveryLogs { get; }
        public bool IgnoreRecoveryLogs { get; }
        public bool AutoStart { get; }
        public uint Network { get; }
        public uint MaxBlockSize { get; }
        public long MaxBlockSystemFee { get; }

        public Settings(IConfigurationSection section)
        {
            RecoveryLogs = section.GetValue("RecoveryLogs", "ConsensusState");
            IgnoreRecoveryLogs = section.GetValue("IgnoreRecoveryLogs", false);
            AutoStart = section.GetValue("AutoStart", false);
            Network = section.GetValue("Network", 5195086u);
            MaxBlockSize = section.GetValue("MaxBlockSize", 262144u);
            MaxBlockSystemFee = section.GetValue("MaxBlockSystemFee", 900000000000L);
        }
    }
}
