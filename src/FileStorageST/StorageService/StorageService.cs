using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.gRPC;
using Neo.FileStorage.Storage.LocalObjectStorage.Engine;
using Neo.FileStorage.Storage.LocalObjectStorage.Shards;
using Neo.FileStorage.Storage.Processors;
using Neo.FileStorage.Storage.Services.Accounting;
using Neo.FileStorage.Storage.Services.Container;
using Neo.FileStorage.Storage.Services.Control;
using Neo.FileStorage.Storage.Services.Control.Service;
using Neo.FileStorage.Storage.Services.Netmap;
using Neo.FileStorage.Storage.Services.Object.Acl;
using Neo.FileStorage.Storage.Services.Reputaion.Service;
using Neo.FileStorage.Storage.Services.Session;
using Neo.FileStorage.Utils;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using Neo.Wallets;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;
using APINetmapService = Neo.FileStorage.API.Netmap.NetmapService;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using APIReputationService = Neo.FileStorage.API.Reputation.ReputationService;
using APISessionService = Neo.FileStorage.API.Session.SessionService;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        public const int ContainerCacheSize = 100;
        public const int ContainerCacheTTLSeconds = 30;
        public const int EACLCacheSize = 100;
        public const int EACLCacheTTLSeconds = 30;
        public ulong CurrentEpoch = 0;
        public API.Netmap.NodeInfo LocalNodeInfo;
        public NetmapStatus NetmapStatus => (NetmapStatus)LocalNodeInfo.State;
        public HealthStatus HealthStatus;
        private readonly ECDsa key;
        private readonly MorphInvoker morphInvoker;
        private readonly NeoSystem system;
        private readonly IActorRef listener;
        private readonly StorageEngine localStorage;
        public ProtocolSettings ProtocolSettings => system.Settings;
        private List<Network.Address> LocalAddresses => LocalNodeInfo.Addresses.Select(p => Network.Address.FromString(p)).ToList();
        private readonly NetmapProcessor netmapProcessor = new();
        private readonly ContainerProcessor containerProcessor = new();
        private readonly List<BlockTimer> blockTimers = new();
        private readonly Server server;

        public StorageService(Wallet wallet, NeoSystem side)
        {
            system = side;
            key = wallet.GetAccounts().First().GetKey().PrivateKey.LoadPrivateKey();
            HealthStatus = HealthStatus.Starting;
            LocalNodeInfo = new()
            {
                PublicKey = ByteString.CopyFrom(key.PublicKey()),
            };
            LocalNodeInfo.Addresses.AddRange(Settings.Default.Addresses);
            LocalNodeInfo.Attributes.AddRange(Settings.Default.Attributes.Select(p =>
            {
                var li = p.Split(":");
                if (li.Length != 2) throw new FormatException("invalid attributes setting");
                return new API.Netmap.NodeInfo.Types.Attribute
                {
                    Key = li[0].Trim(),
                    Value = li[1].Trim(),
                };
            }));
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
            localStorage.Open();
            morphInvoker = new MorphInvoker
            {
                Wallet = wallet,
                NeoSystem = system,
                Blockchain = system.Blockchain,
                BalanceContractHash = Settings.Default.BalanceContractHash,
                ContainerContractHash = Settings.Default.ContainerContractHash,
                NetMapContractHash = Settings.Default.NetmapContractHash,
                ReputationContractHash = Settings.Default.ReputationContractHash,
            };
            listener = system.ActorSystem.ActorOf(Listener.Props("storage"));
            AccountingServiceImpl accountingService = InitializeAccounting();
            ContainerServiceImpl containerService = InitializeContainer();
            ControlServiceImpl controlService = InitializeControl();
            NetmapServiceImpl netmapService = InitializeNetmap();
            ObjectServiceImpl objectService = InitializeObject();
            ReputationServiceImpl reputationService = InitializeReputation();
            SessionServiceImpl sessionService = InitializeSession();
            server = new(Settings.Default.Port);
            server.BindService<APIAccountingService.AccountingServiceBase>(accountingService);
            server.BindService<APIContainerService.ContainerServiceBase>(containerService);
            server.BindService<ControlService.ControlServiceBase>(controlService);
            server.BindService<APINetmapService.NetmapServiceBase>(netmapService);
            server.BindService<APIObjectService.ObjectServiceBase>(objectService);
            server.BindService<APIReputationService.ReputationServiceBase>(reputationService);
            server.BindService<APISessionService.SessionServiceBase>(sessionService);
            server.Start();
            listener.Tell(new Listener.BindProcessorEvent { Processor = netmapProcessor });
            listener.Tell(new Listener.BindProcessorEvent { Processor = containerProcessor });
            listener.Tell(new Listener.BindBlockHandlerEvent
            {
                Handler = block =>
                {
                    TickBlockTimers();
                }
            });
            listener.Tell(new Listener.Start());
            InitState();
            var ni = LocalNodeInfo.Clone();
            ni.State = API.Netmap.NodeInfo.Types.State.Online;
            morphInvoker.AddPeer(ni);
            StartBlockTimers();
            HealthStatus = HealthStatus.Ready;
        }

        private void InitState()
        {
            var epoch = morphInvoker.Epoch();
            var ni = NetmapLocalNodeInfo(epoch);
            if (ni is null)
            {
                Utility.Log(nameof(StorageService), LogLevel.Debug, "could not found node info, offline");
                LocalNodeInfo.State = API.Netmap.NodeInfo.Types.State.Offline;
                return;
            }
            LocalNodeInfo = ni;
            CurrentEpoch = epoch;
        }

        public void SetStatus(NetmapStatus status)
        {
            lock (LocalNodeInfo)
            {
                switch (status)
                {
                    case NetmapStatus.Online:
                        {
                            Interlocked.Exchange(ref reBoostrapTurnedOff, 0);
                            var ni = LocalNodeInfo.Clone();
                            ni.State = API.Netmap.NodeInfo.Types.State.Online;
                            morphInvoker.AddPeer(ni);
                            break;
                        }
                    case NetmapStatus.Offline:
                        {
                            Interlocked.Exchange(ref reBoostrapTurnedOff, 1);
                            morphInvoker.UpdatePeerState(API.Netmap.NodeInfo.Types.State.Offline, key.PublicKey());
                            break;
                        }
                }
            }
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

        public void OnPersisted(Block block, DataCache _2, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
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
                    if (Settings.Default.Contracts.Contains(contract))
                        listener.Tell(new Listener.NewContractEvent() { Notify = notify });
                }
            }
        }

        public void Dispose()
        {
            HealthStatus = HealthStatus.ShuttingDown;
            listener.Tell(new Listener.Stop());
            server.Stop();
            server.Dispose();
            reputationClientCache?.Dispose();
            key?.Dispose();
            localStorage?.Dispose();
        }
    }
}
