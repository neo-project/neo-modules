using Akka.Actor;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Services.Object.Acl;
using Neo.FileStorage.Storage.Services.Object.Delete;
using Neo.FileStorage.Storage.Services.Object.Get;
using Neo.FileStorage.Storage.Services.Object.Get.Remote;
using Neo.FileStorage.Storage.Services.Object.Head;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Object.Search;
using Neo.FileStorage.Storage.Services.Object.Search.Clients;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Police;
using Neo.FileStorage.Storage.Services.Replicate;
using Neo.FileStorage.Storage.Services.Reputaion.Client;
using Neo.FileStorage.Utils;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private ReputationClientCache reputationClientCache;

        private ObjectServiceImpl InitializeObject()
        {
            KeyStore keyStorage = new(key, tokenStore, this);
            reputationClientCache = new()
            {
                EpochSource = this,
                NetmapSource = netmapCache,
                ReputationStorage = trustStorage,
                ClientCache = clientCache,
            };
            IActorRef replicator = system.ActorSystem.ActorOf(Replicator.Props(new()
            {
                RemoteSender = new RemoteSender()
                {
                    KeyStorage = keyStorage,
                    ClientCache = new PutClientCache(reputationClientCache),
                },
                LocalStorage = localStorage,
                PutTimeout = TimeSpan.FromSeconds(Settings.Default.ReplicateTimeout)
            }));
            IActorRef policer = system.ActorSystem.ActorOf(Policer.Props(new()
            {
                LocalInfo = this,
                LocalStorage = localStorage,
                ContainerSoruce = containerCache,
                PlacementBuilder = new NetworkMapBuilder(netmapCache),
                RemoteHeader = new RemoteHeader()
                {
                    KeyStorage = keyStorage,
                    ClientCache = reputationClientCache,
                },
                RedundantCopyCallback = address =>
                {
                    localStorage.Inhume(null, address);
                },
                ReplicatorRef = replicator
            }));
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    policer.Tell(new Policer.Trigger());
                }
            });
            IActorRef localPool = system.ActorSystem.ActorOf(WorkerPool.Props("PutLocal", PutService.DefaultLocalPoolSize));
            IActorRef remotePool = system.ActorSystem.ActorOf(WorkerPool.Props("PutRemote", PutService.DefaultRemotePoolSize));
            PutService putService = new()
            {
                MaxObjectSizeSource = new MaxObjectSizeSource(morphInvoker),
                ContainerSoruce = containerCache,
                LocalObjectStore = localStorage,
                NetmapSource = netmapCache,
                EpochSource = this,
                LocalInfo = this,
                KeyStorage = keyStorage,
                ObjectInhumer = localStorage,
                ClientCache = new PutClientCache(reputationClientCache),
                LocalPool = localPool,
                RemotePool = remotePool
            };
            GetService getService = new()
            {
                Assemble = true,
                KeyStore = keyStorage,
                LocalStorage = localStorage,
                ClientCache = new GetClientCache(reputationClientCache),
                EpochSource = this,
                TraverserGenerator = new TraverserGenerator(netmapCache, containerCache, this, 1),
            };
            SearchService searchService = new()
            {
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                EpochSource = this,
                ClientCache = new SearchClientCache(reputationClientCache),
                TraverserGenerator = new TraverserGenerator(netmapCache, containerCache, this, trackCopies: false),
            };
            DeleteService deleteService = new()
            {
                EpochSource = this,
                PutService = putService,
                SearchService = searchService,
                GetService = getService,
                KeyStorage = keyStorage,
                TombstoneLifetime = Settings.Default.TombstoneLifetime
            };
            return new ObjectServiceImpl
            {
                AclChecker = new()
                {
                    MorphInvoker = morphInvoker,
                    ContainerSource = containerCache,
                    NetmapSource = netmapCache,
                    LocalStorage = localStorage,
                    EAclValidator = new()
                    {
                        EAclStorage = eACLCache,
                    },
                    EpochSource = this,
                },
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        SplitService = new()
                        {
                            ObjectService = new()
                            {
                                GetService = getService,
                                PutService = putService,
                                SearchService = searchService,
                                DeleteService = deleteService
                            }
                        }
                    }
                }
            };
        }
    }
}
