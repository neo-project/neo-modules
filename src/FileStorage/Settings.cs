using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage
{
    public class Settings
    {
        public static Settings Default { get; private set; }
        public uint Network;
        public string WalletPath;
        public string Password;
        public UInt160 NetmapContractHash;
        public UInt160 FsContractHash;
        public UInt160 BalanceContractHash;
        public UInt160 ContainerContractHash;
        public UInt160[] AlphabetContractHash;
        public UInt160 AuditContractHash;
        public UInt160 FsIdContractHash;
        public ECPoint[] validators;

        public int NetmapContractWorkersSize;
        public int FsContractWorkersSize;
        public int BalanceContractWorkersSize;
        public int ContainerContractWorkersSize;
        public int AlphabetContractWorkersSize;

        public int PdpPoolSize;
        public int PorPoolSize;
        public ulong MaxPDPSleepInterval;
        public int QueueCapacity;

        public string[] Urls;
        public long EpochDuration;
        public long AlphabetDuration;
        public int MintEmitCacheSize;
        public ulong MintEmitThreshold;
        public long GasBalanceThreshold;
        public long MintEmitValue;
        public ulong StorageEmission;
        public bool CleanupEnabled;
        public ulong CleanupThreshold;
        public bool IsSender;
        public ulong SearchTimeout;
        public ulong HeadTimeout;
        public ulong RangeTimeout;
        public TimeSpan IndexerTimeout;

        public List<UInt160> Contracts = new List<UInt160>();

        private Settings(IConfigurationSection section)
        {
            this.Urls = section.GetSection("URLs").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.Network = section.GetValue("Network", 5195086u);
            this.WalletPath = section.GetSection("WalletPath").Value;
            this.Password = section.GetSection("Password").Value;

            IConfigurationSection contracts = section.GetSection("contracts");
            this.NetmapContractHash = UInt160.Parse(contracts.GetSection("netmap").Value);
            this.FsContractHash = UInt160.Parse(contracts.GetSection("neofs").Value);
            this.FsIdContractHash = UInt160.Parse(contracts.GetSection("neofsId").Value);
            this.BalanceContractHash = UInt160.Parse(contracts.GetSection("balance").Value);
            this.ContainerContractHash = UInt160.Parse(contracts.GetSection("container").Value);
            this.AuditContractHash = UInt160.Parse(contracts.GetSection("audit").Value);
            this.AlphabetContractHash = contracts.GetSection("alphabet").GetChildren().Select(p => UInt160.Parse(p.Get<string>())).ToArray();
            Contracts.Add(NetmapContractHash);
            Contracts.Add(FsContractHash);
            Contracts.Add(FsIdContractHash);
            Contracts.Add(BalanceContractHash);
            Contracts.Add(ContainerContractHash);
            Contracts.AddRange(AlphabetContractHash);

            this.validators = section.GetSection("votes").GetChildren().Select(p => ECPoint.FromBytes(p.Get<string>().HexToBytes(), ECCurve.Secp256r1)).ToArray();

            IConfigurationSection workSizes = section.GetSection("workers");
            this.NetmapContractWorkersSize = int.Parse(workSizes.GetSection("netmap").Value);
            this.FsContractWorkersSize = int.Parse(workSizes.GetSection("neofs").Value);
            this.BalanceContractWorkersSize = int.Parse(workSizes.GetSection("balance").Value);
            this.ContainerContractWorkersSize = int.Parse(workSizes.GetSection("container").Value);
            this.AlphabetContractWorkersSize = int.Parse(workSizes.GetSection("alphabet").Value);

            IConfigurationSection timers = section.GetSection("timers");
            this.EpochDuration = long.Parse(timers.GetSection("epoch").Value);
            this.AlphabetDuration = long.Parse(timers.GetSection("emit").Value);

            IConfigurationSection emit = section.GetSection("emit");
            this.MintEmitCacheSize = int.Parse(emit.GetSection("mint").GetSection("cache_size").Value);
            this.MintEmitThreshold = ulong.Parse(emit.GetSection("mint").GetSection("threshold").Value);
            this.MintEmitValue = long.Parse(emit.GetSection("mint").GetSection("value").Value);
            this.GasBalanceThreshold = long.Parse(emit.GetSection("gas").GetSection("balance_threshold").Value);
            this.StorageEmission = ulong.Parse(emit.GetSection("storage").GetSection("amount").Value);

            IConfigurationSection netmapCleaner = section.GetSection("netmap_cleaner");
            this.CleanupEnabled = bool.Parse(netmapCleaner.GetSection("enabled").Value);
            this.CleanupThreshold = ulong.Parse(netmapCleaner.GetSection("threshold").Value);

            this.IsSender = bool.Parse(section.GetSection("isSender").Value);

            IConfigurationSection audit = section.GetSection("audit");
            this.SearchTimeout = ulong.Parse(audit.GetSection("timeout").GetSection("get").Value);
            this.HeadTimeout = ulong.Parse(audit.GetSection("timeout").GetSection("head").Value);
            this.RangeTimeout = ulong.Parse(audit.GetSection("timeout").GetSection("rangehash").Value);
            this.PdpPoolSize = int.Parse(audit.GetSection("pdp").GetSection("pairs_pool_size").Value);
            this.PorPoolSize = int.Parse(audit.GetSection("por").GetSection("pool_size").Value);
            this.MaxPDPSleepInterval = ulong.Parse(audit.GetSection("pdp").GetSection("max_sleep_interval").Value);
            this.QueueCapacity = int.Parse(audit.GetSection("task").GetSection("queue_capacity").Value);

            IConfigurationSection indexer = section.GetSection("indexer");
            this.IndexerTimeout = TimeSpan.FromMilliseconds(long.Parse(audit.GetSection("cache_timeout").Value));
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
