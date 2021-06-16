using System;
using Akka.Actor;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Container.Announcement;
using Neo.FileStorage.Services.Object.Acl;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.Police;
using Neo.FileStorage.Services.Replicate;
using Neo.FileStorage.Services.Reputaion.Local.Client;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        private ObjectServiceImpl InitializeObject(StorageEngine localStorage)
        {
            KeyStorage keyStorage = new(key, tokenStore);
            ClientCache clientCache = new();
            ReputationClientCache reputationClientCache = new()
            {
                StorageNode = this,
                BasicCache = clientCache,
                MorphClient = morphClient,
                ReputationStorage = new(),
            };
            IActorRef replicator = system.ActorSystem.ActorOf(Replicator.Props(new()
            {
                RemoteSender = new()
                {
                    KeyStorage = keyStorage,
                    ClientCache = reputationClientCache,
                },
                LocalStorage = localStorage,
            }));
            IActorRef policeRef = system.ActorSystem.ActorOf(Policer.Props(new()
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
                ReplicatorRef = replicator
            }));

            TTLNetworkCache<ContainerID, FSContainer> containerCache = new(ContainerCacheSize, TimeSpan.FromSeconds(ContainerCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetContainer(cid);
            });
            TTLNetworkCache<ContainerID, EACLTable> eaclCache = new(EACLCacheSize, TimeSpan.FromSeconds(EACLCacheTTLSeconds), cid =>
            {
                return morphClient.InvokeGetEACL(cid)?.Table;
            });

            LoadPlacementBuilder loadPlacementBuilder = new()
            {
                MorphClient = morphClient,
            };

            LocalObjectInhumer objInhumer = new()
            {
                LocalStorage = localStorage,
            };
            var remover = new LocalObjectRemover
            {
                LocalStorage = localStorage,
            };
            PutService putService = new()
            {
                MorphClient = morphClient,
                LocalAddress = LocalAddress,
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                ObjectInhumer = objInhumer,
                ClientCache = reputationClientCache,
            };
            GetService getService = new()
            {
                Assemble = true,
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                ClientCache = reputationClientCache,
                MorphClient = morphClient,
                TraverserGenerator = new(morphClient, LocalAddress, 1),
            };
            SearchService searchService = new()
            {
                KeyStorage = keyStorage,
                LocalStorage = localStorage,
                MorphClient = morphClient,
                ClientCache = reputationClientCache,
                TraverserGenerator = new(morphClient, LocalAddress, trackCopies: false),
            };
            return new ObjectServiceImpl
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
            };
        }
    }
}
