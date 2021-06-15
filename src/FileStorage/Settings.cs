using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;
using Neo.FileStorage.LocalObjectStorage.Blob;

namespace Neo.FileStorage
{
    public class Settings
    {
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
                    Path = "Data_WriteCache",
                    MaxMemorySize = 1ul << 30,
                    MaxObjectSize = 64ul << 20,
                    SmallObjectSize = 32ul << 10,
                    MaxDBSize = 0ul//TODO
                };
            }

            public static WriteCacheSettings Load(IConfigurationSection section)
            {
                return new()
                {
                    Path = section.GetValue("Path", "Data_WriteCache"),
                    MaxMemorySize = section.GetValue("MaxMemorySize", 1ul << 30),
                    MaxObjectSize = section.GetValue("MaxObjectSize", 64ul << 20),
                    SmallObjectSize = section.GetValue("SmallObjectSize", 32ul << 10),
                    MaxDBSize = section.GetValue("MaxDBSize", 0ul)//TODO
                };
            }
        }

        public class BlobovniczaSettings
        {
            public ulong Size;
            public int ShallowDepth;
            public int ShallowWidth;
            public int OpenCacheSize;

            public static BlobovniczaSettings Default { get; private set; }

            static BlobovniczaSettings()
            {
                Default = new()
                {
                    Size = 1ul << 30,
                    ShallowDepth = 2,
                    ShallowWidth = 16,
                    OpenCacheSize = 16
                };
            }

            public static BlobovniczaSettings Load(IConfigurationSection section)
            {
                return new()
                {
                    Size = section.GetValue("Size", 1ul << 30),
                    ShallowDepth = section.GetValue("ShallowDepth", 2),
                    ShallowWidth = section.GetValue("ShallowWidth", 16),
                    OpenCacheSize = section.GetValue("OpenCacheSize", 16)
                };
            }
        }

        public class BlobStorageSettings
        {
            public string Path;
            public bool Compress;
            public int ShallowDepth;
            public ulong SmallSizeLimit;
            public BlobovniczaSettings BlobovniczaSettings;

            public static BlobStorageSettings Default { get; private set; }

            static BlobStorageSettings()
            {
                Default = new()
                {
                    Path = "Data_BlobStorage",
                    Compress = true,
                    ShallowDepth = 4,
                    SmallSizeLimit = 1ul << 20,
                    BlobovniczaSettings = BlobovniczaSettings.Default
                };
            }

            public static BlobStorageSettings Load(IConfigurationSection section)
            {
                return new()
                {
                    Path = section.GetValue("Path", "Data_BlobStorage"),
                    Compress = section.GetValue("Compress", true),
                    ShallowDepth = section.GetValue("ShallowDepth", 4),
                    SmallSizeLimit = section.GetValue("SmallSizeLimit", 1ul << 20),
                    BlobovniczaSettings = BlobovniczaSettings.Load(section.GetSection("Blobovnicza"))
                };
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
                    Path = "Data_Metabase"
                };
            }

            public static MetabaseSettings Load(IConfigurationSection section)
            {
                return new()
                {
                    Path = section.GetValue("Path", "Data_Metabase")
                };
            }
        }

        public class ShardSettings
        {
            public bool UseWriteCache;
            public WriteCacheSettings WriteCacheSettings;
            public BlobStorageSettings BlobStorageSettings;
            public MetabaseSettings MetabaseSettings;

            public static ShardSettings Default { get; private set; }

            static ShardSettings()
            {
                Default = new()
                {
                    UseWriteCache = true,
                    WriteCacheSettings = WriteCacheSettings.Default,
                    BlobStorageSettings = BlobStorageSettings.Default,
                    MetabaseSettings = MetabaseSettings.Default,
                };
            }

            public static ShardSettings Load(IConfigurationSection section)
            {
                ShardSettings settings = new();
                settings.UseWriteCache = section.GetValue("UseWriteCache", true);
                if (settings.UseWriteCache)
                    settings.WriteCacheSettings = WriteCacheSettings.Load(section.GetSection("WriteCache"));
                settings.BlobStorageSettings = BlobStorageSettings.Load(section.GetSection("BlobStorage"));
                settings.MetabaseSettings = MetabaseSettings.Load(section.GetSection("Metabase"));
                return settings;
            }
        }

        public class StorageSettings
        {
            public ShardSettings[] Shards;

            public static StorageSettings Default { get; private set; }

            static StorageSettings()
            {
                Default = new()
                {
                    Shards = new ShardSettings[] { ShardSettings.Default }
                };
            }

            public static StorageSettings Load(IConfigurationSection section)
            {
                return new()
                {
                    Shards = section.GetChildren().Select(p => ShardSettings.Load(p)).ToArray(),
                };
            }
        }

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
        public bool IsSender;
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
        public StorageSettings LocalStorageSettings;
        public List<UInt160> Contracts = new();

        private Settings(IConfigurationSection section)
        {
            SideChainConfig = section.GetValue("SideChainConfig", "config.neofs.mainnet.json");
            AutoStart = section.GetValue("AutoStartInnerRing", false);
            AsInnerRing = section.GetValue("AutoStartInnerRing", false);
            AsStorage = section.GetValue("AutoStartStorage", true);
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

            IsSender = bool.Parse(section.GetSection("IsSender").Value);

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

            IConfigurationSection fee = section.GetSection("Fee");
            MainChainFee = fee.GetValue("MainChain", 5000L);
            SideChainFee = fee.GetValue("SideChain", 5000L);

            LocalStorageSettings = StorageSettings.Load(section.GetSection("Storage"));
            if (!LocalStorageSettings.Shards.Any()) LocalStorageSettings = StorageSettings.Default;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
