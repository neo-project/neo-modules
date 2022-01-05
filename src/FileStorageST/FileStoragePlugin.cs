using Neo.ConsoleService;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.FileStorage.Storage
{
    public partial class StoragePlugin : Plugin, IPersistencePlugin
    {
        public event EventHandler<Wallet> WalletChanged;
        public override string Name => "StorageService";
        public override string Description => "Provide distributed file storage service";
        public NeoSystem MorphSystem;
        public StorageService StorageService;
        private IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList;
        private IWalletProvider walletProvider;
        private Wallet wallet;
        private Wallet CurrentWallet
        {
            get
            {
                if (wallet is null) wallet = walletProvider?.GetWallet();
                if (wallet is null) throw new InvalidOperationException("Please open wallet first");
                return wallet;
            }
            set
            {
                wallet = value;
            }
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (MorphSystem is null)
            {
                MorphSystem = system;
                MorphSystem.ServiceAdded += NeoSystem_ServiceAdded;
            }
        }

        public void OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network == MorphSystem?.Settings.Network)
            {
                this.applicationExecutedList = applicationExecutedList;
            }
        }

        public void OnCommit(NeoSystem system, Block block, DataCache snapshot)
        {
            if (system.Settings.Network == MorphSystem?.Settings.Network)
            {
                StorageService?.OnPersisted(block, applicationExecutedList);
            }
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex) => true;

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider)
            {
                walletProvider = service as IWalletProvider;
                MorphSystem.ServiceAdded -= NeoSystem_ServiceAdded;
                if (Settings.Default.AutoStart)
                {
                    walletProvider.WalletChanged += WalletProvider_WalletChanged;
                }
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet w)
        {
            CurrentWallet = w;
            if (Settings.Default.AutoStart) Start();
        }

        private void Start()
        {
            if (MorphSystem is null)
            {
                Console.WriteLine("Neo system not initialized");
                return;
            }
            if (StorageService is not null)
            {
                Console.WriteLine("Storage service already started");
                return;
            }
            try
            {
                StorageService = new(CurrentWallet, MorphSystem);
            }
            catch (Exception e)
            {
                Console.WriteLine($"couldn't start storage serivce, error={e}");
            }
        }

        [ConsoleCommand("fs start storage", Category = "FileStorageService", Description = "Start as storage node")]
        private void OnStartStorage()
        {
            Start();
        }

        [ConsoleCommand("fs balance", Category = "FileStorageService", Description = "Show balance")]
        private void OnGetBanlance()
        {
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem,
            };
            if (Settings.Default.BalanceContractHash is null)
            {
                Settings.Default.BalanceContractHash = invoker.NNSContractScriptHash(MorphInvoker.BalanceContractName);
            }
            invoker.BalanceContractHash = Settings.Default.BalanceContractHash;
            var balance = invoker.BalanceOf(CurrentWallet.GetAccounts().First().ScriptHash.ToArray());
            byte dec = (byte)invoker.BalanceDecimals();
            var divisor = BigInteger.Pow(10, dec);
            var b = new decimal(balance) / (decimal)divisor;
            Console.WriteLine($"balance: {b}");
        }

        [ConsoleCommand("fs config", Category = "FileStorageService", Description = "Get neofs config parameter in contract")]
        private void OnGetConfig()
        {
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem
            };
            if (Settings.Default.NetmapContractHash is null)
            {
                Settings.Default.NetmapContractHash = invoker.NNSContractScriptHash(MorphInvoker.NetmapContractName);
            }
            invoker.NetMapContractHash = Settings.Default.NetmapContractHash;
            var parameters = new string[] { "MaxObjectSize", "BasicIncomeRate", "AuditFee", "EpochDuration", "ContainerFee", "EigenTrustIterations", "EigenTrustAlpha", "InnerRingCandidateFee", "WithdrawFee" };
            foreach (var name in parameters)
            {
                var method = invoker.GetType().GetMethod(name);
                var value = method.Invoke(invoker, null);
                Console.WriteLine($"{name}: {value}");
            }
        }

        [ConsoleCommand("fs node info", Category = "FileStorageService", Description = "Show node state")]
        private void OnGetInfo()
        {
            if (StorageService is null)
            {
                Console.WriteLine("StorageService not started.");
                return;
            }
            Console.WriteLine(StorageService.NodeInfo);
        }

        [ConsoleCommand("fs node state", Category = "FileStorageService", Description = "Show node state")]
        private void OnGetState()
        {
            if (StorageService is null)
            {
                Console.WriteLine("StorageService not started.");
                return;
            }
            Console.WriteLine(StorageService.NodeInfo.State);
        }

        [ConsoleCommand("fs node offline", Category = "FileStorageService", Description = "Show node state")]
        private void OnOffline()
        {
            if (StorageService is null)
            {
                Console.WriteLine("StorageService not started.");
                return;
            }
            StorageService.UpdateState();
        }

        [ConsoleCommand("fs epoch", Category = "FileStorageService", Description = "Show current epoch")]
        private void OnGetEpoch()
        {
            if (StorageService is not null)
                Console.WriteLine(StorageService.CurrentEpoch);
            else
            {
                var invoker = new MorphInvoker
                {
                    Wallet = CurrentWallet,
                    NeoSystem = MorphSystem,
                };
                if (Settings.Default.NetmapContractHash is null)
                {
                    Settings.Default.NetmapContractHash = invoker.NNSContractScriptHash(MorphInvoker.NetmapContractName);
                }
                invoker.NetMapContractHash = Settings.Default.NetmapContractHash;
                Console.WriteLine(invoker.Epoch());
            }
        }

        [ConsoleCommand("fs onchain container sizes", Category = "FileStorageService", Description = "Show onchain container sizes")]
        private void OnOnchainContainerSize()
        {
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem
            };
            if (Settings.Default.NetmapContractHash is null)
            {
                Settings.Default.NetmapContractHash = invoker.NNSContractScriptHash(MorphInvoker.NetmapContractName);
            }
            if (Settings.Default.ContainerContractHash is null)
            {
                Settings.Default.ContainerContractHash = invoker.NNSContractScriptHash(MorphInvoker.ContainerContractName);
            }
            invoker.ContainerContractHash = Settings.Default.ContainerContractHash;
            invoker.NetMapContractHash = Settings.Default.NetmapContractHash;
            var epoch = invoker.Epoch() - 1;
            Console.WriteLine($"Epoch: {epoch}");
            var ids = invoker.ListSizes(epoch);
            foreach (var id in ids)
            {
                var estimations = invoker.GetContainerSize(id);
                Console.WriteLine($"ContainerID: {estimations.ContainerID.String()}");
                if (estimations.AllEstimation.Count > 0)
                {
                    foreach (var e in estimations.AllEstimation)
                        Console.WriteLine($"reporter={e.Reporter.PublicKeyToScriptHash().ToAddress(MorphSystem.Settings.AddressVersion)}, size={e.Size}");
                }
            }
        }

        [ConsoleCommand("fs local container sizes", Category = "FileStorageService", Description = "Show local container sizes")]
        private void OnLocalContainerSizes()
        {
            if (StorageService is null)
            {
                Console.WriteLine("StorageService not started.");
                return;
            }
            var engine = StorageService.Engine;
            foreach (var cid in engine.ListContainers())
                Console.WriteLine($"{cid.String()} {engine.ContainerSize(cid)}");
        }

        [ConsoleCommand("fs container nodes", Category = "FileStorageService", Description = "Show container nodes")]
        private void OnContainerNodes(string containerId)
        {
            var cid = ContainerID.FromString(containerId);
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem,
                NetMapContractHash = Settings.Default.NetmapContractHash,
                ContainerContractHash = Settings.Default.ContainerContractHash,
            };
            if (Settings.Default.NetmapContractHash is null)
            {
                Settings.Default.NetmapContractHash = invoker.NNSContractScriptHash(MorphInvoker.NetmapContractName);
            }
            if (Settings.Default.ContainerContractHash is null)
            {
                Settings.Default.ContainerContractHash = invoker.NNSContractScriptHash(MorphInvoker.ContainerContractName);
            }
            invoker.ContainerContractHash = Settings.Default.ContainerContractHash;
            invoker.NetMapContractHash = Settings.Default.NetmapContractHash;
            var container = invoker.GetContainer(cid)?.Container;
            if (container is null) throw new InvalidOperationException("container not found");
            var nm = invoker.GetNetMapByDiff(0);
            var nss = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            if (nss.Flatten().Count > 0)
                Console.WriteLine(" node                               address");
            foreach (var ns in nss)
            {
                if (ns.Count == 0)
                {
                    Console.WriteLine("[]");
                    continue;
                }
                Console.WriteLine("[");
                foreach (var n in ns)
                {
                    Console.WriteLine($" {n.PublicKey.PublicKeyToScriptHash().ToAddress(MorphSystem.Settings.AddressVersion)} {n.Addresses.FirstOrDefault()}");
                }
                Console.WriteLine("]");
            }
        }

        [ConsoleCommand("fs container info", Category = "FileStorageService", Description = "Show container nodes")]
        private void OnContainerInfo(string containerId)
        {
            var cid = ContainerID.FromString(containerId);
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem
            };
            if (Settings.Default.ContainerContractHash is null)
            {
                Settings.Default.ContainerContractHash = invoker.NNSContractScriptHash(MorphInvoker.ContainerContractName);
            }
            invoker.ContainerContractHash = Settings.Default.ContainerContractHash;
            var container = invoker.GetContainer(cid)?.Container;
            if (container is null) throw new InvalidOperationException("container not found");
            Console.WriteLine($"Container: {container}");
        }

        [ConsoleCommand("fs node map", Category = "FileStorageService", Description = "Show online nodes")]
        private void OnNodeMap()
        {
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem,

            };
            if (Settings.Default.NetmapContractHash is null)
            {
                Settings.Default.NetmapContractHash = invoker.NNSContractScriptHash(MorphInvoker.NetmapContractName);
            }
            invoker.NetMapContractHash = Settings.Default.NetmapContractHash;
            var nm = invoker.GetNetMapByDiff(0);
            var nss = nm.Nodes;
            Console.WriteLine($"Count: {nss.Count}");
            foreach (var n in nss)
                Console.WriteLine(n.Info);
        }

        [ConsoleCommand("fs list objects", Category = "FileStorageService", Description = "List all stored objects")]
        private void OnListObject()
        {
            if (StorageService is null)
            {
                Console.WriteLine("StorageService not started.");
                return;
            }
            var addresses = StorageService.Engine.List(100);
            Console.WriteLine($"Count: {addresses.Count}, truncated: {addresses.Count == 100}");
            foreach (var address in addresses.OrderBy(p => p.String()))
            {
                var header = StorageService.Engine.Head(address);
                Console.WriteLine($"{address.String()} {header.ObjectType} {header.PayloadSize}");
            }
        }

        [ConsoleCommand("fs contracts", Category = "FileStorageService", Description = "List all neofs contracts")]
        private void OnContracts()
        {
            var invoker = new MorphInvoker
            {
                Wallet = CurrentWallet,
                NeoSystem = MorphSystem,
            };
            foreach (var (name, hash) in Settings.Default.Contracts)
            {
                if (hash is null)
                    Settings.Default.Contracts[name] = invoker.NNSContractScriptHash(name);
                Console.WriteLine($"{name} {Settings.Default.Contracts[name]}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            StorageService?.Dispose();
        }
    }
}
