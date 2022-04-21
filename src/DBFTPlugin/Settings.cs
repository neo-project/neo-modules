// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
            MaxBlockSystemFee = section.GetValue("MaxBlockSystemFee", 150000000000L);
        }
    }
}
