using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Accounting;
using Neo.FileStorage.Services.Container;
using Neo.FileStorage.Services.Container.Announcement;
using Neo.FileStorage.Services.Container.Announcement.Control;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Storage;
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
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;
using APINetmapService = Neo.FileStorage.API.Netmap.NetmapService;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using APISessionService = Neo.FileStorage.API.Session.SessionService;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage
{
    public sealed class StorageService : IDisposable
    {
        public const int ContainerCacheSize = 100;
        public const int ContainerCacheTTLSeconds = 30;
        public const int EACLCacheSize = 100;
        public const int EACLCacheTTLSeconds = 30;
        public const int NetmapCacheSize = 10;

        public ProtocolSettings ProtocolSettings;
        private readonly ECDsa key;
        private readonly Client morphClient;
        private readonly Wallet wallet;
        private readonly NeoSystem system;
        public ulong CurrentEpoch;
        public API.Netmap.NodeInfo LocalNodeInfo;
        public NetmapStatus NetmapStatus = NetmapStatus.Online;
        public HealthStatus HealthStatus = HealthStatus.Ready;

        private Network.Address LocalAddress => Network.Address.AddressFromString(LocalNodeInfo.Address);

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
            var containerCache = new TTLNetworkCache<ContainerID, FSContainer>(ContainerCacheSize, TimeSpan.FromSeconds(ContainerCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetContainer(cid);
            });
            var eaclCache = new TTLNetworkCache<ContainerID, EACLTable>(EACLCacheSize, TimeSpan.FromSeconds(EACLCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetEACL(cid)?.Table;
            });
            var netmapCache = new TTLNetworkCache<ulong, NetMap>(NetmapCacheSize, TimeSpan.FromSeconds(ContainerCacheTTLSeconds), epoch =>
            {
                return morphClient.InvokeEpochSnapshot(epoch);
            });
            //Audit
            var loadAccumulator = new AnnouncementStorage();
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
        }

        public void OnSidePersisted(Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {

        }

        public void Dispose()
        {
            key.Dispose();
        }
    }
}
