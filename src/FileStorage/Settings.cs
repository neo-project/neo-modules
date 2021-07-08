using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.LocalObjectStorage.Shards;

namespace Neo.FileStorage
{
    public class Settings
    {
        public static Settings Default { get; private set; }
        public bool AutoStart;
        public string SideChainConfig;
        public bool AsInnerRing;
        public bool AsStorage;
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
        public int SettlementWorkersSize;
        public int GovernanceWorkersSize;
        public int AuditContractWorkersSize;

        public int PdpPoolSize;
        public int PorPoolSize;
        public ulong MaxPDPSleepInterval;
        public int QueueCapacity;
        public int AuditTaskPoolSize;

        public uint EpochDuration;
        public uint AlphabetDuration;
        public int MintEmitCacheSize;
        public ulong MintEmitThreshold;
        public long GasBalanceThreshold;
        public long MintEmitValue;
        public ulong StorageEmission;
        public bool CleanupEnabled;
        public ulong CleanupThreshold;
        public int SearchTimeout;
        public int GetTimeout;
        public int HeadTimeout;
        public int RangeTimeout;
        public TimeSpan IndexerTimeout;
        public uint StopEstimationDMul;
        public uint StopEstimationDDiv;
        public uint CollectBasicIncomeMul;
        public uint CollectBasicIncomeDiv;
        public uint DistributeBasicIncomeMul;
        public uint DistributeBasicIncomeDiv;
        public ulong BasicIncomeRate;
        public long MainChainFee;
        public long SideChainFee;
        public long AuditFee;
        public StorageNodeSettings StorageSettings;
        public List<UInt160> Contracts = new();

        private Settings(IConfigurationSection section)
        {
            SideChainConfig = section.GetValue("SideChainConfig", "config.neofs.mainnet.json");
            AutoStart = section.GetValue("AutoStart", false);
            AsInnerRing = section.GetValue("AsInnerRing", false);
            AsStorage = section.GetValue("AsStorage", true);
            WalletPath = section.GetSection("WalletPath").Value;
            Password = section.GetSection("Password").Value;

            IConfigurationSection contracts = section.GetSection("Contracts");
            NetmapContractHash = UInt160.Parse(contracts.GetSection("Netmap").Value);
            FsContractHash = UInt160.Parse(contracts.GetSection("NeoFS").Value);
            FsIdContractHash = UInt160.Parse(contracts.GetSection("NeoFSID").Value);
            BalanceContractHash = UInt160.Parse(contracts.GetSection("Balance").Value);
            ContainerContractHash = UInt160.Parse(contracts.GetSection("Container").Value);
            AuditContractHash = UInt160.Parse(contracts.GetSection("Audit").Value);
            ReputationContractHash = UInt160.Parse(contracts.GetSection("Reputation").Value);
            AlphabetContractHash = contracts.GetSection("Alphabet").GetChildren().Select(p => UInt160.Parse(p.Get<string>())).ToArray();
            Contracts.Add(NetmapContractHash);
            Contracts.Add(FsContractHash);
            Contracts.Add(FsIdContractHash);
            Contracts.Add(BalanceContractHash);
            Contracts.Add(ContainerContractHash);
            Contracts.AddRange(AlphabetContractHash);

            validators = section.GetSection("Votes").GetChildren().Select(p => ECPoint.FromBytes(p.Get<string>().HexToBytes(), ECCurve.Secp256r1)).ToArray();

            IConfigurationSection workSizes = section.GetSection("Workers");
            NetmapContractWorkersSize = workSizes.GetValue("Netmap", 10);
            FsContractWorkersSize = workSizes.GetValue("NeoFS", 10);
            BalanceContractWorkersSize = workSizes.GetValue("Balance", 10);
            ContainerContractWorkersSize = workSizes.GetValue("Container", 10);
            AlphabetContractWorkersSize = workSizes.GetValue("Alphabet", 10);
            ReputationContractWorkersSize = workSizes.GetValue("Reputation", 10);
            SettlementWorkersSize = workSizes.GetValue("Settlement", 10);
            GovernanceWorkersSize = workSizes.GetValue("Governance", 10);
            AuditContractWorkersSize = workSizes.GetValue("AuditContract", 10);

            IConfigurationSection timers = section.GetSection("Timers");
            EpochDuration = timers.GetValue("Epoch", 0u);
            AlphabetDuration = timers.GetValue("Emit", 0u);
            StopEstimationDMul = timers.GetSection("StopEstimation").GetValue("Mul", 1u);
            StopEstimationDDiv = timers.GetSection("StopEstimation").GetValue("Div", 1u);
            CollectBasicIncomeMul = timers.GetSection("CollectBasicIncome").GetValue("Mul", 1u);
            CollectBasicIncomeDiv = timers.GetSection("CollectBasicIncome").GetValue("Div", 1u);
            DistributeBasicIncomeMul = timers.GetSection("DistributeBasicIncome").GetValue("Mul", 1u);
            DistributeBasicIncomeDiv = timers.GetSection("DistributeBasicIncome").GetValue("Div", 1u);

            IConfigurationSection emit = section.GetSection("Emit");
            MintEmitCacheSize = emit.GetSection("Mint").GetValue("CacheSize", 1000);
            MintEmitThreshold = emit.GetSection("Mint").GetValue("Threshold", 1ul);
            MintEmitValue = emit.GetSection("Mint").GetValue("Value", 20000000);
            GasBalanceThreshold = emit.GetSection("Gas").GetValue("BalanceThreshold", 0);
            StorageEmission = emit.GetSection("Storage").GetValue("Amount", 0ul);

            IConfigurationSection netmapCleaner = section.GetSection("NetmapCleaner");
            CleanupEnabled = netmapCleaner.GetValue("Eenabled", false);
            CleanupThreshold = netmapCleaner.GetValue("Threshold", 3ul);

            IConfigurationSection audit = section.GetSection("Audit");
            SearchTimeout = audit.GetSection("Timeout").GetValue("Search", 10000);
            GetTimeout = audit.GetSection("Timeout").GetValue("Get", 5000);
            HeadTimeout = audit.GetSection("Timeout").GetValue("Head", 5000);
            RangeTimeout = audit.GetSection("Timeout").GetValue("RangeHash", 5000);
            PdpPoolSize = audit.GetSection("POR").GetValue("PoolSize", 10);
            PorPoolSize = audit.GetSection("PDP").GetValue("PoolSize", 10);
            MaxPDPSleepInterval = audit.GetSection("PDP").GetValue("MaxSleepInterval", 5000ul);
            QueueCapacity = audit.GetSection("Task").GetValue("QueueCapacity", 100);
            AuditTaskPoolSize = audit.GetSection("Task").GetValue("PoolSize", 10);

            IConfigurationSection indexer = section.GetSection("Indexer");
            IndexerTimeout = TimeSpan.FromMilliseconds(indexer.GetValue("CacheTimeout", 15000));

            IConfigurationSection settlement = section.GetSection("Settlement");
            BasicIncomeRate = settlement.GetValue("BasicIncomeRate", 0ul);
            AuditFee = settlement.GetValue("AuditFee", 0L);

            IConfigurationSection fee = section.GetSection("Fee");
            MainChainFee = fee.GetValue("MainChain", 5000L);
            SideChainFee = fee.GetValue("SideChain", 5000L);

            StorageSettings = StorageNodeSettings.Load(section.GetSection("Storage"));
            if (!StorageSettings.Shards.Any()) StorageSettings = StorageNodeSettings.Default;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }

    public class WriteCacheSettings
    {
        public string Path;
        public ulong MaxMemorySize;
        public ulong MaxObjectSize;
        public ulong SmallObjectSize;
        public ulong MaxDBSize;

        public static WriteCacheSettings Default { get; private set; }

        static WriteCacheSettings()
        {
            Default = new()
            {
                Path = $"Data_WriteCache_{Guid.NewGuid()}",
                MaxMemorySize = WriteCache.DefaultMemorySize,
                MaxObjectSize = WriteCache.DefaultMaxObjectSize,
                SmallObjectSize = WriteCache.DefaultSmallObjectSize,
                MaxDBSize = 0ul//TODO
            };
        }

        public static WriteCacheSettings Load(IConfigurationSection section)
        {
            WriteCacheSettings settings = new()
            {
                Path = section.GetValue("Path", ""),
                MaxMemorySize = section.GetValue("MaxMemorySize", WriteCache.DefaultMemorySize),
                MaxObjectSize = section.GetValue("MaxObjectSize", WriteCache.DefaultMaxObjectSize),
                SmallObjectSize = section.GetValue("SmallObjectSize", WriteCache.DefaultSmallObjectSize),
                MaxDBSize = section.GetValue("MaxDBSize", 0ul)//TODO
            };
            if (settings.Path == "") throw new FormatException("invalid writecache path");
            return settings;
        }
    }

    public class BlobovniczasSettings
    {
        public ulong Size;
        public int ShallowDepth;
        public int ShallowWidth;
        public int OpenCacheSize;

        public static BlobovniczasSettings Default { get; private set; }

        static BlobovniczasSettings()
        {
            Default = new()
            {
                Size = Blobovnicza.DefaultFullSizeLimit,
                ShallowDepth = BlobovniczaTree.DefaultBlzShallowDepth,
                ShallowWidth = BlobovniczaTree.DefaultBlzShallowWidth,
                OpenCacheSize = BlobovniczaTree.DefaultOpenedCacheSize,
            };
        }

        public static BlobovniczasSettings Load(IConfigurationSection section)
        {
            return new()
            {
                Size = section.GetValue("Size", Blobovnicza.DefaultFullSizeLimit),
                ShallowDepth = section.GetValue("ShallowDepth", BlobovniczaTree.DefaultBlzShallowDepth),
                ShallowWidth = section.GetValue("ShallowWidth", BlobovniczaTree.DefaultBlzShallowWidth),
                OpenCacheSize = section.GetValue("OpenCacheSize", BlobovniczaTree.DefaultOpenedCacheSize)
            };
        }
    }

    public class BlobStorageSettings
    {
        public string Path;
        public bool Compress;
        public int ShallowDepth;
        public ulong SmallSizeLimit;
        public BlobovniczasSettings BlobovniczasSettings;

        public static BlobStorageSettings Default { get; private set; }

        static BlobStorageSettings()
        {
            Default = new()
            {
                Path = $"Data_BlobStorage_{Guid.NewGuid()}",
                Compress = true,
                ShallowDepth = FSTree.DefaultShallowDepth,
                SmallSizeLimit = BlobStorage.DefaultSmallSizeLimit,
                BlobovniczasSettings = BlobovniczasSettings.Default
            };
        }

        public static BlobStorageSettings Load(IConfigurationSection section)
        {
            BlobStorageSettings settings = new()
            {
                Path = section.GetValue("Path", ""),
                Compress = section.GetValue("Compress", true),
                ShallowDepth = section.GetValue("ShallowDepth", FSTree.DefaultShallowDepth),
                SmallSizeLimit = section.GetValue("SmallSizeLimit", BlobStorage.DefaultSmallSizeLimit),
                BlobovniczasSettings = BlobovniczasSettings.Load(section.GetSection("Blobovnicza"))
            };
            if (settings.Path == "") throw new FormatException("invalid blobstorage path");
            return settings;
        }
    }

    public class MetabaseSettings
    {
        public string Path;

        public static MetabaseSettings Default { get; private set; }

        static MetabaseSettings()
        {
            Default = new()
            {
                Path = $"Data_Metabase{Guid.NewGuid()}"
            };
        }

        public static MetabaseSettings Load(IConfigurationSection section)
        {
            MetabaseSettings settings = new()
            {
                Path = section.GetValue("Path", "")
            };
            if (settings.Path == "") throw new FormatException("invalid metabase path");
            return settings;
        }
    }

    public class ShardSettings
    {
        public bool UseWriteCache;
        public int RemoverInterval;
        public int RemoveBatchSize;
        public WriteCacheSettings WriteCacheSettings;
        public BlobStorageSettings BlobStorageSettings;
        public MetabaseSettings MetabaseSettings;

        public static ShardSettings Default { get; private set; }

        static ShardSettings()
        {
            Default = new()
            {
                UseWriteCache = Shard.DefaultUseWriteCache,
                RemoverInterval = Shard.DefaultRemoveInterval,
                RemoveBatchSize = Shard.DefaultRemoveBatchSize,
                WriteCacheSettings = WriteCacheSettings.Default,
                BlobStorageSettings = BlobStorageSettings.Default,
                MetabaseSettings = MetabaseSettings.Default,
            };
        }

        public static ShardSettings Load(IConfigurationSection section)
        {
            ShardSettings settings = new();
            settings.UseWriteCache = section.GetValue("UseWriteCache", Shard.DefaultUseWriteCache);
            if (settings.UseWriteCache)
                settings.WriteCacheSettings = WriteCacheSettings.Load(section.GetSection("WriteCache"));
            settings.RemoverInterval = section.GetValue("RemoverInterval", Shard.DefaultRemoveInterval);
            settings.RemoveBatchSize = section.GetValue("RemoveBatchSize", Shard.DefaultRemoveBatchSize);
            settings.BlobStorageSettings = BlobStorageSettings.Load(section.GetSection("BlobStorage"));
            settings.MetabaseSettings = MetabaseSettings.Load(section.GetSection("Metabase"));
            return settings;
        }
    }

    public class StorageNodeSettings
    {
        public const string DefaultAddress = "/ip4/0.0.0.0/tcp/8080";
        public const int DefaultPort = 8080;
        public string Address;
        public List<string> Attributes;
        public int Port;
        public ShardSettings[] Shards;

        public static StorageNodeSettings Default { get; private set; }

        static StorageNodeSettings()
        {
            Default = new()
            {
                Address = DefaultAddress,
                Attributes = new(),
                Port = DefaultPort,
                Shards = new ShardSettings[] { ShardSettings.Default },
            };
        }

        public static StorageNodeSettings Load(IConfigurationSection section)
        {
            return new()
            {
                Address = section.GetValue("Address", DefaultAddress),
                Attributes = section.GetSection("Attributes").GetChildren().Select(p => p.ToString()).ToList(),
                Port = section.GetValue("Port", DefaultPort),
                Shards = section.GetSection("Shards").GetChildren().Select(p => ShardSettings.Load(p)).ToArray(),
            };
        }
    }
}
