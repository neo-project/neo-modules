using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;

namespace Neo.FileStorage
{
    public class Settings
    {
        public static Settings Default { get; private set; }
        public uint MainNetwork;
        public uint SideNetwork;
        public string SideChainConfigPath;
        public string SideChainStorageEngine;
        public bool StartInnerRing;
        public bool StartStorage;
        public string WalletPath;
        public string Password;
        public UInt160 NetmapContractHash;
        public UInt160 FsContractHash;
        public UInt160 BalanceContractHash;
        public UInt160 ContainerContractHash;
        public UInt160[] AlphabetContractHash;
        public UInt160 AuditContractHash;
        public UInt160 FsIdContractHash;
        public UInt160 ReputationContractHash;
        public ECPoint[] validators;

        public int NetmapContractWorkersSize;
        public int FsContractWorkersSize;
        public int BalanceContractWorkersSize;
        public int ContainerContractWorkersSize;
        public int AlphabetContractWorkersSize;
        public int ReputationContractWorkersSize;

        public int PdpPoolSize;
        public int PorPoolSize;
        public ulong MaxPDPSleepInterval;
        public int QueueCapacity;
        public int AuditTaskPoolSize;

        public string[] Urls;
        public uint EpochDuration;
        public uint AlphabetDuration;
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
        public uint StopEstimationDMul;
        public uint StopEstimationDDiv;
        public uint CollectBasicIncomeMul;
        public uint CollectBasicIncomeDiv;
        public uint DistributeBasicIncomeMul;
        public uint DistributeBasicIncomeDiv;
        public ulong BasicIncomeRate;

        public List<UInt160> Contracts = new();

        private Settings(IConfigurationSection section)
        {
            this.Urls = section.GetSection("URLs").GetChildren().Select(p => p.Get<string>()).ToArray();
            this.MainNetwork = section.GetValue("MainNetwork", 5195086u);
            this.SideNetwork = section.GetValue("SideNetwork", 0u);
            this.SideChainConfigPath = section.GetValue("SideChainConfigPath", "./FileStorage/sidechain.json");
            this.SideChainStorageEngine = section.GetValue("SideChainStorageEngine", "LevelDBStore");
            this.StartInnerRing = section.GetValue("StartInnerRing", false);
            this.StartStorage = section.GetValue("StartStorage", true);
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
            this.ReputationContractWorkersSize = int.Parse(workSizes.GetSection("reputation").Value);

            IConfigurationSection timers = section.GetSection("timers");
            this.EpochDuration = uint.Parse(timers.GetSection("epoch").Value);
            this.AlphabetDuration = uint.Parse(timers.GetSection("emit").Value);
            //this.StopEstimationDMul = uint.Parse(timers.GetSection("stop_estimation").GetSection("mul").Value);
            //this.StopEstimationDDiv = uint.Parse(timers.GetSection("stop_estimation").GetSection("div").Value);
            //this.CollectBasicIncomeMul = uint.Parse(timers.GetSection("collect_basic_income").GetSection("mul").Value);
            //this.CollectBasicIncomeDiv = uint.Parse(timers.GetSection("collect_basic_income").GetSection("div").Value);
            //this.DistributeBasicIncomeMul = uint.Parse(timers.GetSection("distribute_basic_income").GetSection("mul").Value);
            //this.DistributeBasicIncomeDiv = uint.Parse(timers.GetSection("distribute_basic_income").GetSection("div").Value);

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
            this.AuditTaskPoolSize = int.Parse(audit.GetSection("task").GetSection("pool_size").Value);

            IConfigurationSection indexer = section.GetSection("indexer");
            this.IndexerTimeout = TimeSpan.FromMilliseconds(long.Parse(indexer.GetSection("cache_timeout").Value));

            IConfigurationSection settlement = section.GetSection("settlement");
            this.BasicIncomeRate = ulong.Parse(settlement.GetSection("basic_income_rate").Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
