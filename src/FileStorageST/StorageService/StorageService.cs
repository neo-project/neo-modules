using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Control;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Cache;
using Neo.FileStorage.Storage.RpcServer;
using Neo.FileStorage.Storage.LocalObjectStorage.Engine;
using Neo.FileStorage.Storage.LocalObjectStorage.Shards;
using Neo.FileStorage.Storage.Processors;
using Neo.FileStorage.Storage.Services.Accounting;
using Neo.FileStorage.Storage.Services.Container;
using Neo.FileStorage.Storage.Services.Control;
using Neo.FileStorage.Storage.Services.Netmap;
using Neo.FileStorage.Storage.Services.Object.Acl;
using Neo.FileStorage.Storage.Services.Reputaion.Service;
using Neo.FileStorage.Storage.Services.Session;
using Neo.FileStorage.Utils;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;
using APINetmapService = Neo.FileStorage.API.Netmap.NetmapService;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using APIReputationService = Neo.FileStorage.API.Reputation.ReputationService;
using APISessionService = Neo.FileStorage.API.Session.SessionService;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IEpochSource, ILocalInfoSource, IDisposable
    {
        public ProtocolSettings ProtocolSettings => system.Settings;
        public ulong CurrentEpoch => currentEpoch;
        public byte[] PublicKey { get; private set; }

        public API.Netmap.NodeInfo NodeInfo
        {
            get
            {
                if (localNodeInfo is null) return Settings.Default.LocalNodeInfo.Clone();
                return localNodeInfo.Clone();
            }
        }

        public uint Network => system.Settings.Network;
        public HealthStatus HealthStatus { get; private set; }

        public StorageEngine Engine => localStorage;

        private API.Netmap.NodeInfo localNodeInfo;
        private readonly ECDsa key;
        private readonly MorphInvoker morphInvoker;
        private readonly NeoSystem system;
        private readonly IActorRef listener;
        private readonly StorageEngine localStorage;
        private readonly NetmapProcessor netmapProcessor;
        private readonly ContainerProcessor containerProcessor;
        private readonly List<BlockTimer> blockTimers = new();
        private readonly HashSet<UInt160> contracts;
        private readonly Wallet wallet;
        private readonly Server server;
        private readonly NetmapCache netmapCache;
        private ulong currentEpoch;

        public StorageService(Wallet w, NeoSystem side)
        {
            system = side;
            wallet = w;
            key = wallet.GetAccounts().First().GetKey().PrivateKey.LoadPrivateKey();
            HealthStatus = HealthStatus.Starting;
            PublicKey = key.PublicKey();
            Settings.Default.LocalNodeInfo.PublicKey = ByteString.CopyFrom(PublicKey);
            LookupContractsInNNS();
            contracts = Settings.Default.Contracts.Values.ToHashSet();
            netmapProcessor = new(Settings.Default.NetmapContractHash);
            containerProcessor = new(Settings.Default.ContainerContractHash);
            localStorage = new();
            int i = 0;
            foreach (var shardSettings in Settings.Default.Shards)
            {
                var shard = new Shard(shardSettings, system.ActorSystem.ActorOf(WorkerPool.Props($"Shard{i}", 2)), localStorage.ProcessExpiredTomstones);
                netmapProcessor.AddEpochHandler(p =>
                {
                    if (p is NewEpochEvent e)
                    {
                        shard.OnNewEpoch(e.EpochNumber);
                    }
                });
                localStorage.AddShard(shard);
                i++;
            }
            morphInvoker = new MorphInvoker
            {
                Wallet = wallet,
                NeoSystem = system,
                Blockchain = system.Blockchain,
                SideChainFee = Settings.Default.SideChainFee,
                BalanceContractHash = Settings.Default.BalanceContractHash,
                ContainerContractHash = Settings.Default.ContainerContractHash,
                NetMapContractHash = Settings.Default.NetmapContractHash,
                ReputationContractHash = Settings.Default.ReputationContractHash,
            };
            listener = system.ActorSystem.ActorOf(Listener.Props(nameof(StorageService)));
            netmapCache = new(NetmapCache.DefaultCapacity, this, morphInvoker);
            SessionServiceImpl sessionService = InitializeSession();
            AccountingServiceImpl accountingService = InitializeAccounting();
            ContainerServiceImpl containerService = InitializeContainer();
            ControlServiceImpl controlService = InitializeControl();
            NetmapServiceImpl netmapService = InitializeNetmap();
            ObjectServiceImpl objectService = InitializeObject();
            ReputationServiceImpl reputationService = InitializeReputation();
            server = new(Settings.Default.GrpcSettings);
            server.BindService<APIAccountingService.AccountingServiceBase>(accountingService);
            server.BindService<APIContainerService.ContainerServiceBase>(containerService);
            server.BindService<ControlService.ControlServiceBase>(controlService);
            server.BindService<APINetmapService.NetmapServiceBase>(netmapService);
            server.BindService<APIObjectService.ObjectServiceBase>(objectService);
            server.BindService<APIReputationService.ReputationServiceBase>(reputationService);
            server.BindService<APISessionService.SessionServiceBase>(sessionService);
            listener.Tell(new Listener.BindProcessorEvent { Processor = netmapProcessor });
            listener.Tell(new Listener.BindProcessorEvent { Processor = containerProcessor });
            listener.Tell(new Listener.BindBlockHandlerEvent
            {
                Handler = block =>
                {
                    TickBlockTimers();
                }
            });
            InitState();
            var ni = Settings.Default.LocalNodeInfo.Clone();
            ni.State = API.Netmap.NodeInfo.Types.State.Online;
            morphInvoker.AddPeer(ni);
            StartBlockTimers();
            HealthStatus = HealthStatus.Ready;
            listener.Tell(new Listener.Start());
            server.Start();
            localStorage.Open();
        }

        public void LookupContractsInNNS()
        {
            var invoker = new MorphInvoker
            {
                NeoSystem = system,
                Wallet = wallet,
            };
            foreach (var (name, scriptHash) in Settings.Default.Contracts)
            {
                if (scriptHash is null)
                {
                    var hash = invoker.NNSContractScriptHash(name);
                    Settings.Default.Contracts[name] = hash;
                }
            }
        }

        private void InitState()
        {
            var epoch = morphInvoker.Epoch();
            var ni = NetmapLocalNodeInfo(epoch);
            if (ni is null)
            {
                Utility.Log(nameof(StorageService), LogLevel.Debug, "could not found node info, offline");
                localNodeInfo = Settings.Default.LocalNodeInfo.Clone();
                localNodeInfo.State = API.Netmap.NodeInfo.Types.State.Offline;
            }
            else
            {
                localNodeInfo = ni;
            }
            Interlocked.Exchange(ref currentEpoch, epoch);
            Utility.Log(nameof(StorageService), LogLevel.Debug, $"initial network state, epoch={epoch}, state={localNodeInfo.State}");
        }

        public void SetStatus(NetmapStatus status)
        {
            if ((int)localNodeInfo.State == (int)status) return;
            lock (localNodeInfo)
            {
                switch (status)
                {
                    case NetmapStatus.Online:
                        {
                            Interlocked.Exchange(ref needBootstrap, 1);
                            var ni = Settings.Default.LocalNodeInfo.Clone();
                            ni.State = API.Netmap.NodeInfo.Types.State.Online;
                            morphInvoker.AddPeer(ni);
                            break;
                        }
                    case NetmapStatus.Offline:
                        {
                            Interlocked.Exchange(ref needBootstrap, 0);
                            UpdateState();
                            break;
                        }
                }
            }
        }

        public void UpdateState()
        {
            morphInvoker.UpdatePeerState(API.Netmap.NodeInfo.Types.State.Offline, key.PublicKey());
        }

        private void StartBlockTimers()
        {
            foreach (var t in blockTimers)
                t.Reset();
        }

        private void TickBlockTimers()
        {
            foreach (var t in blockTimers)
                t.Tick();
        }

        public void OnPersisted(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (listener is null) return;
            listener.Tell(new Listener.NewBlockEvent { Block = block });
            foreach (var appExec in applicationExecutedList)
            {
                Transaction tx = appExec.Transaction;
                VMState state = appExec.VMState;
                if (tx is null || state != VMState.HALT) continue;
                var notifys = appExec.Notifications;
                if (notifys is null) continue;
                foreach (var notify in notifys)
                {
                    var contract = notify.ScriptHash;
                    if (contracts.Contains(contract))
                        listener.Tell(new Listener.NewContractEvent() { Notify = notify });
                }
            }
        }

        public void Dispose()
        {
            HealthStatus = HealthStatus.ShuttingDown;
            listener?.Tell(new Listener.Stop());
            server?.Stop();
            server?.Dispose();
            reputationClientCache?.Dispose();
            key?.Dispose();
            localStorage?.Dispose();
        }
    }
}
