using Akka.Actor;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Accounting;
using Neo.FileStorage.Services.Container;
using Neo.FileStorage.Services.Container.Announcement;
using Neo.FileStorage.Services.Container.Announcement.Control;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Storage;
using Neo.FileStorage.Services.Control;
using Neo.FileStorage.Services.Control.Service;
using Neo.FileStorage.Services.Netmap;
using Neo.FileStorage.Services.Object.Acl;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.Police;
using Neo.FileStorage.Services.Replicate;
using Neo.FileStorage.Services.Reputaion.Local.Client;
using Neo.FileStorage.Services.Session;
using Neo.FileStorage.Services.Session.Storage;
using Neo.FileStorage.Storage;
using Neo.FileStorage.Storage.Processors;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;
using APINetmapService = Neo.FileStorage.API.Netmap.NetmapService;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using APISessionService = Neo.FileStorage.API.Session.SessionService;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        public const int ContainerCacheSize = 100;
        public const int ContainerCacheTTLSeconds = 30;
        public const int EACLCacheSize = 100;
        public const int EACLCacheTTLSeconds = 30;
        public ulong CurrentEpoch;
        public API.Netmap.NodeInfo LocalNodeInfo;
        public NetmapStatus NetmapStatus = NetmapStatus.Online;
        public HealthStatus HealthStatus = HealthStatus.Ready;
        private readonly ECDsa key;
        private readonly Client morphClient;
        private readonly Wallet wallet;
        private readonly NeoSystem system;
        private readonly IActorRef listener;
        public ProtocolSettings ProtocolSettings => system.Settings;
        private Network.Address LocalAddress => Network.Address.AddressFromString(LocalNodeInfo.Address);
        private NetmapProcessor netmapProcessor = new();
        private ContainerProcessor containerProcessor = new();

        public StorageService(NeoSystem side)
        {
            system = side;
            StorageEngine localStorage = new();
            morphClient = new Client
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                }
            };
            TTLNetworkCache<ContainerID, FSContainer> containerCache = new(ContainerCacheSize, TimeSpan.FromSeconds(ContainerCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetContainer(cid);
            });
            TTLNetworkCache<ContainerID, EACLTable> eaclCache = new(EACLCacheSize, TimeSpan.FromSeconds(EACLCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetEACL(cid)?.Table;
            });
            listener = system.ActorSystem.ActorOf(Listener.Props("storage"));
            netmapProcessor.AddEpochParser(MorphEvent.NewEpochEvent.ParseNewEpochEvent);

            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is MorphEvent.NewEpochEvent e)
                {
                    Interlocked.Exchange(ref CurrentEpoch, e.EpochNumber);
                }
            });
            listener.Tell(new Listener.BindProcessorEvent { processor = netmapProcessor });
            AnnouncementStorage loadAccumulator = new();
            Controller controller = new()
            {
                LocalMetrics = new SimpleProvider(new LocalStorageLoad
                {
                    LocalStorage = localStorage,
                }),
                AnnouncementAccumulator = new SimpleProvider(loadAccumulator),
                LocalAnnouncementTarget = new LoadRouter
                {
                    LocalNodeInfo = LocalNodeInfo,
                    RemoteProvider = new RemoteLoadAnnounceProvider
                    {
                        Key = key,
                        LocalAddress = LocalAddress,
                        ClientCache = new ClientCache(),
                        DeadEndProvider = new SimpleProvider(loadAccumulator),
                    },
                    RouteBuilder = new()
                    {
                        PlacementBuilder = new()
                        {
                            MorphClient = morphClient,
                        }
                    }
                },
                ResultReceiver = new SimpleProvider(new MorphLoadWriter
                {
                    PublicKey = key.PublicKey(),
                    MorphClient = morphClient,
                })
            };
            containerProcessor.AddStartEstimateContainerParser(MorphEvent.StartEstimationEvent.ParseStartEstimationEvent);
            containerProcessor.AddStartEstimateHandler(p =>
            {
                if (p is MorphEvent.StartEstimationEvent e)
                {
                    controller.Start(e.Epoch);
                }
            });
            containerProcessor.AddStopEstimateContainerParser(MorphEvent.StopEstimationEvent.ParseStopEstimationEvent);
            containerProcessor.AddStopEstimateHandler(p =>
            {
                if (p is MorphEvent.StopEstimationEvent e)
                {
                    controller.Stop(e.Epoch);
                }
            });
            listener.Tell(new Listener.BindProcessorEvent { processor = containerProcessor });

            //Audit

            ControlService.BindService(new ControlServiceImpl
            {
                Key = key,
                LocalStorage = localStorage,
                MorphClient = morphClient,
                StorageNode = this,
            });

            APIAccountingService.BindService(new AccountingServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        AccountingService = new()
                        {
                            MorphClient = morphClient,
                        }
                    }
                }
            });

            var loadPlacementBuilder = new LoadPlacementBuilder
            {
                MorphClient = morphClient,
            };
            APIContainerService.BindService(new ContainerServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        ContainerService = new()
                        {
                            MorphClient = morphClient,
                            UsedSpaceService = new()
                            {
                                Key = key,
                                LocalNodeInfo = LocalNodeInfo,
                                Router = new()
                                {
                                    RemoteProvider = new()
                                    {
                                        Key = key,
                                        LocalAddress = LocalAddress,
                                        ClientCache = new ClientCache(),
                                        DeadEndProvider = new SimpleProvider(loadAccumulator),
                                    },
                                    RouteBuilder = new()
                                    {
                                        PlacementBuilder = loadPlacementBuilder
                                    },
                                    LocalNodeInfo = LocalNodeInfo,
                                },
                                Loadbuilder = loadPlacementBuilder,
                                RouteBuilder = new()
                                {
                                    PlacementBuilder = loadPlacementBuilder,
                                }
                            }
                        }
                    }
                }
            });

            APINetmapService.BindService(new NetmapServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        NetmapService = new()
                        {
                            StorageNode = this,
                        }
                    }
                }
            });

            var objInhumer = new LocalObjectInhumer
            {
                LocalStorage = localStorage,
            };
            var clientCache = new ClientCache();
            var reputationClientCache = new ReputaionClientCache
            {
                StorageNode = this,
                BasicCache = clientCache,
                MorphClient = morphClient,
                ReputationStorage = new(),
            };
            var tokenStore = new TokenStore();
            var keyStorage = new KeyStorage(key, tokenStore);
            var putService = new PutService
            {
                MorphClient = morphClient,
                LocalAddress = LocalAddress,
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                ObjectInhumer = objInhumer,
                ClientCache = reputationClientCache,
            };
            var getService = new GetService
            {
                Assemble = true,
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                ClientCache = reputationClientCache,
                MorphClient = morphClient,
                TraverserGenerator = new(morphClient, LocalAddress, 1),
            };
            var searchService = new SearchService
            {
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                MorphClient = morphClient,
                ClientCache = reputationClientCache,
                TraverserGenerator = new(morphClient, LocalAddress, trackCopies: false),
            };
            APIObjectService.BindService(new ObjectServiceImpl
            {
                AclChecker = new(),
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        SplitService = new()
                        {
                            ObjectService = new()
                            {
                                GetService = getService,
                                PutService = putService,
                                SearchService = searchService,
                                DeleteService = new()
                                {
                                    MorphClient = morphClient,
                                    PutService = putService,
                                    SearchService = searchService,
                                    GetService = getService,
                                    KeyStorage = keyStorage,
                                },
                            }
                        }
                    }
                }
            });

            APISessionService.BindService(new SessionServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        SessionService = new()
                        {
                            TokenStore = tokenStore
                        }
                    }
                }
            });

            var remover = new LocalObjectRemover
            {
                LocalStorage = localStorage,
            };
            var gcRef = Services.ObjectManager.GC.GC.Props(remover);

            var replicatorRef = system.ActorSystem.ActorOf(Replicator.Props(new()
            {
                RemoteSender = new()
                {
                    KeyStorage = keyStorage,
                    ClientCache = reputationClientCache,
                },
                LocalStorage = localStorage,
            }));

            var policeRef = system.ActorSystem.ActorOf(Policer.Props(new()
            {
                LocalAddress = LocalAddress,
                LocalStorage = localStorage,
                MorphClient = morphClient,
                PlacementBuilder = new NetworkMapBuilder(morphClient),
                RemoteHeader = new()
                {
                    KeyStorage = keyStorage,
                    ClientCache = reputationClientCache,
                },
                RedundantCopyCallback = address =>
                {
                    localStorage.Inhume(null, address);
                },
                ReplicatorRef = replicatorRef
            }));
            listener.Tell(new Listener.Start());
        }

        public void OnPersisted(Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
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
                        listener.Tell(new Listener.NewContractEvent() { notify = notify });
                }
            }
        }

        public void Dispose()
        {
            key.Dispose();
        }
    }
}
