using Microsoft.Extensions.Configuration;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.Storage.LocalObjectStorage.Engine;
using Neo.FileStorage.Storage.LocalObjectStorage.Shards;
using Neo.FileStorage.Storage.Services.Object.Delete;
using Neo.FileStorage.Storage.Services.Replicate;
using Neo.FileStorage.Storage.Services.Session.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage
{
    public class Settings
    {
        public const int DefaultPort = 8080;
        public static Settings Default { get; private set; }
        public bool AutoStart;
        public ulong BootstrapInterval;
        public UInt160 NetmapContractHash
        {
            get
            {
                return Contracts[MorphInvoker.NetmapContractName];
            }
            set
            {
                Contracts[MorphInvoker.NetmapContractName] = value;
            }
        }
        public UInt160 BalanceContractHash
        {
            get
            {
                return Contracts[MorphInvoker.BalanceContractName];
            }
            set
            {
                Contracts[MorphInvoker.BalanceContractName] = value;
            }
        }
        public UInt160 ContainerContractHash
        {
            get
            {
                return Contracts[MorphInvoker.ContainerContractName];
            }
            set
            {
                Contracts[MorphInvoker.ContainerContractName] = value;
            }
        }
        public UInt160 ReputationContractHash
        {
            get
            {
                return Contracts[MorphInvoker.ReputationContractName];
            }
            set
            {
                Contracts[MorphInvoker.ReputationContractName] = value;
            }
        }
        public Dictionary<string, UInt160> Contracts = new()
        {
            { MorphInvoker.NetmapContractName, null },
            { MorphInvoker.BalanceContractName, null },
            { MorphInvoker.ContainerContractName, null },
            { MorphInvoker.ReputationContractName, null }
        };
        public GrpcSettings GrpcSettings;
        public NodeInfo LocalNodeInfo;
        public long SideChainFee;
        public ulong TombstoneLifetime;
        public int ReplicateTimeout;
        public LocalStorageSettings StorageSettings = LocalStorageSettings.Default;
        public List<byte[]> Administrators = new();

        private Settings(IConfigurationSection section)
        {
            AutoStart = section.GetValue("AutoStart", false);
            BootstrapInterval = section.GetValue("BootstrapInterval", StorageService.DefaultBootstrapInterval);
            var contract_section = section.GetSection("Contracts");
            if (contract_section is not null)
            {
                if (UInt160.TryParse(contract_section.GetSection("Netmap")?.Value, out var netmapContractHash))
                    NetmapContractHash = netmapContractHash;
                if (UInt160.TryParse(contract_section.GetSection("Balance")?.Value, out var balanceContractHash))
                    BalanceContractHash = balanceContractHash;
                if (UInt160.TryParse(contract_section.GetSection("Container")?.Value, out var containerContractHash))
                    ContainerContractHash = containerContractHash;
                if (UInt160.TryParse(contract_section.GetSection("Reputation")?.Value, out var reputationContractHash))
                    ReputationContractHash = reputationContractHash;
            }
            var grpc_section = section.GetSection("Grpc");
            if (grpc_section is null)
                throw new InvalidOperationException("no grpc settings");
            GrpcSettings = GrpcSettings.Load(grpc_section);
            var ni_section = section.GetSection("NodeInfo");
            if (ni_section is null)
                throw new InvalidOperationException("no node info settings");
            SideChainFee = section.GetValue("SideChainFee", 5000L);
            TombstoneLifetime = section.GetValue("TombstoneLifetime", DeleteService.DefaultTomestoneLifetime);
            var admin_section = section.GetSection("Administrators");
            if (admin_section is not null)
                Administrators.AddRange(admin_section.GetChildren().Select(p => p.Get<string>()).Distinct().Select(p => p.HexToBytes()));
            var replicate = section.GetSection("Replicate");
            if (replicate is not null)
                ReplicateTimeout = replicate.GetValue("Timeout", Replicator.Args.DefaultPutTimeout);
            else
                ReplicateTimeout = Replicator.Args.DefaultPutTimeout;
            var storage_section = section.GetSection("LocalStorage");
            if (storage_section is not null)
                StorageSettings = LocalStorageSettings.Load(storage_section);
            var addresses = ni_section.GetSection("Addresses").GetChildren().Select(p => p.Value).ToArray();
            var attributes = ni_section.GetSection("Attributes").GetChildren().Select(p => p.Value).ToList();
            LocalNodeInfo = new();
            LocalNodeInfo.Addresses.AddRange(addresses);
            Dictionary<string, string> attrs = new();
            string key, value;
            string[] li;
            foreach (var attr in attributes)
            {
                li = attr.Split(":");
                if (li.Length != 2) throw new FormatException("Invalid attributes setting");
                key = li[0].Trim();
                if (key.Length == 0) throw new FormatException("Invalid attributes setting");
                value = li[1].Trim();
                if (value.Length == 0) throw new FormatException("Invalid attributes setting");
                attrs[key] = value;
            }
            LocalNodeInfo.Attributes.AddRange(attrs.Select(p => new NodeInfo.Types.Attribute
            {
                Key = p.Key,
                Value = p.Value,
            }));
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }

    public class GrpcSettings
    {
        public int Port;
        public string SslCert;
        public string SslCertPassword;
        public bool LogEnabled;
        public string SessionStorePath;

        public static GrpcSettings Load(IConfigurationSection section)
        {
            return new()
            {
                Port = section.GetValue("Port", Settings.DefaultPort),
                SslCert = section.GetValue("SslCert", ""),
                SslCertPassword = section.GetValue("SslCertPassword", ""),
                LogEnabled = section.GetValue("LogEnabled", true),
                SessionStorePath = section.GetValue("SessionStorePath", TokenStore.DefaultSessionStorePath)
            };
        }
    }

    public class WriteCacheSettings
    {
        public ulong MaxCacheSize;
        public ulong MaxMemorySize;
        public ulong MaxObjectSize;
        public ulong SmallObjectSize;
        public static WriteCacheSettings Default { get; private set; }

        static WriteCacheSettings()
        {
            Default = new()
            {
                MaxCacheSize = WriteCache.DefaultMaxCacheSize,
                MaxMemorySize = WriteCache.DefaultMemorySize,
                MaxObjectSize = WriteCache.DefaultMaxObjectSize,
                SmallObjectSize = WriteCache.DefaultSmallObjectSize,
            };
        }

        public static WriteCacheSettings Load(IConfigurationSection section)
        {
            WriteCacheSettings settings = new()
            {
                MaxCacheSize = section.GetValue("MaxCacheSize", WriteCache.DefaultMaxCacheSize),
                MaxMemorySize = section.GetValue("MaxMemorySize", WriteCache.DefaultMemorySize),
                MaxObjectSize = section.GetValue("MaxObjectSize", WriteCache.DefaultMaxObjectSize),
                SmallObjectSize = section.GetValue("SmallObjectSize", WriteCache.DefaultSmallObjectSize),
            };
            return settings;
        }
    }

    public class BlobovniczasSettings
    {
        public ulong BlobSize;
        public byte ShallowDepth;
        public byte ShallowWidth;
        public byte OpenCacheSize;
        public static BlobovniczasSettings Default { get; private set; }

        static BlobovniczasSettings()
        {
            Default = new()
            {
                BlobSize = Blobovnicza.DefaultFullSizeLimit,
                ShallowDepth = BlobovniczaTree.DefaultBlzShallowDepth,
                ShallowWidth = BlobovniczaTree.DefaultBlzShallowWidth,
                OpenCacheSize = BlobovniczaTree.DefaultOpenedCacheSize,
            };
        }

        public static BlobovniczasSettings Load(IConfigurationSection section)
        {
            return new()
            {
                BlobSize = section.GetValue("BlobSize", Blobovnicza.DefaultFullSizeLimit),
                ShallowDepth = section.GetValue("ShallowDepth", BlobovniczaTree.DefaultBlzShallowDepth),
                ShallowWidth = section.GetValue("ShallowWidth", BlobovniczaTree.DefaultBlzShallowWidth),
                OpenCacheSize = section.GetValue("OpenCacheSize", BlobovniczaTree.DefaultOpenedCacheSize)
            };
        }
    }

    public class FSTreeSettings
    {
        public int ShallowDepth;
        public int DirectoryNameLength;
        public static FSTreeSettings Default { get; private set; }

        static FSTreeSettings()
        {
            Default = new()
            {
                ShallowDepth = FSTree.DefaultShallowDepth,
                DirectoryNameLength = FSTree.DefaultDirNameLength
            };
        }

        public static FSTreeSettings Load(IConfigurationSection section)
        {
            FSTreeSettings settings = new()
            {
                ShallowDepth = section.GetValue("ShallowDepth", FSTree.DefaultShallowDepth),
                DirectoryNameLength = section.GetValue("DirectoryNameLength", FSTree.DefaultDirNameLength)
            };
            return settings;
        }
    }

    public class BlobStorageSettings
    {
        public bool Compress;
        public string[] CompressExcludeContentTypes;
        public ulong SmallSizeLimit;
        public FSTreeSettings FSTreeSettings;
        public BlobovniczasSettings BlobovniczasSettings;
        public static BlobStorageSettings Default { get; private set; }

        static BlobStorageSettings()
        {
            Default = new()
            {
                Compress = true,
                CompressExcludeContentTypes = new string[] { "video/*", "audio/*" },
                SmallSizeLimit = BlobStorage.DefaultSmallSizeLimit,
                FSTreeSettings = FSTreeSettings.Default,
                BlobovniczasSettings = BlobovniczasSettings.Default
            };
        }

        public static BlobStorageSettings Load(IConfigurationSection section)
        {
            var compress_section = section.GetSection("Compress");
            BlobStorageSettings settings = new()
            {
                Compress = compress_section?.GetValue("Enable", true) ?? false,
                CompressExcludeContentTypes = compress_section?.GetSection("ExcludeContentTypes").GetChildren().Select(p => p.Value).ToArray() ?? Array.Empty<string>(),
                SmallSizeLimit = section.GetValue("SmallSizeLimit", BlobStorage.DefaultSmallSizeLimit),
                FSTreeSettings = FSTreeSettings.Load(section.GetSection("FSTree")),
                BlobovniczasSettings = BlobovniczasSettings.Load(section.GetSection("Blobovniczas"))
            };
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
            return settings;
        }
    }

    public class LocalStorageSettings
    {
        public string Path;
        public List<ShardSettings> Shards;
        public static LocalStorageSettings Default { get; private set; }

        static LocalStorageSettings()
        {
            Default = new()
            {
                Path = StorageEngine.DefaultPath,
                Shards = new() { ShardSettings.Default }
            };
        }

        public static LocalStorageSettings Load(IConfigurationSection section)
        {
            var settings = new LocalStorageSettings()
            {
                Path = section.GetValue("Path", StorageEngine.DefaultPath)
            };
            var shard_section = section.GetSection("Shards");
            if (shard_section is null)
                settings.Shards = new() { ShardSettings.Default };
            else
                settings.Shards = section.GetSection("Shards").GetChildren().Select(p => ShardSettings.Load(p)).ToList();
            return settings;
        }
    }
}
