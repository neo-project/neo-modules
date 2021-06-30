using System;
using Akka.Actor;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Services.Reputaion.Common;
using Neo.FileStorage.Services.Reputaion.Common.Route;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Calculate;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Route;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Services.Reputaion.EigenTrust.Storage.Daughters;
using Neo.FileStorage.Services.Reputaion.Intermediate;
using Neo.FileStorage.Services.Reputaion.Local;
using Neo.FileStorage.Services.Reputaion.Local.Routes;
using Neo.FileStorage.Services.Reputaion.Local.Storage;
using Neo.FileStorage.Services.Reputaion.Service;
using Neo.FileStorage.Utils;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        public const int DefaultReputationWorkPoolSize = 32;

        private ReputationServiceImpl InitializeReputation()
        {
            IActorRef workPool = system.ActorSystem.ActorOf(WorkerPool.Props("Reputation", DefaultReputationWorkPoolSize));
            NetmapCache netmapCache = new(this, morphClient);
            TrustStorage trustStorage = new();
            DaughtersStorage daughterStorage = new();
            ConsumersStorage consumersStorage = new();
            ConsumerStorageWriterProvider consumerStorageWriterProvider = new()
            {
                Storage = consumersStorage
            };
            DaughterStorageWriterProvider daughterStorageWriterProvider = new()
            {
                Storage = daughterStorage
            };
            LocalTrustStorage localTrustStorage = new()
            {
                TrustStorage = trustStorage,
                NetmapCache = netmapCache,
                LocalKey = key.PublicKey()
            };
            ManagerBuilder managerBuilder = new()
            {
                NetmapCache = netmapCache
            };
            LocalRouteBuilder localRouteBuilder = new()
            {
                ManagerBuilder = managerBuilder
            };
            IntermediateRouteBuilder intermediateRouteBuilder = new()
            {
                ManagerBuilder = managerBuilder
            };
            ClientCache clientCache = new();
            RemoteTrustProvider remoteLocalTrustProvider = new()
            {
                LocalAddress = LocalAddress,
                DeadEndProvider = daughterStorageWriterProvider,
                ClientCache = clientCache,
                RemoteProvider = new Services.Reputaion.Local.Remote.RemoteProvider
                {
                    Key = key
                }
            };
            RemoteTrustProvider remoteIntermediateTrustProvider = new()
            {
                LocalAddress = LocalAddress,
                DeadEndProvider = consumerStorageWriterProvider,
                ClientCache = clientCache,
                RemoteProvider = new Services.Reputaion.Intermediate.Remote.RemoteProvider
                {
                    Key = key
                }
            };
            Router localTrustRouter = new()
            {
                LocalNodeInfo = LocalNodeInfo,
                RemoteWriterProvider = remoteLocalTrustProvider,
                RouteBuilder = localRouteBuilder
            };
            Router intermediateTrustRouter = new()
            {
                LocalNodeInfo = LocalNodeInfo,
                RemoteWriterProvider = remoteIntermediateTrustProvider,
                RouteBuilder = intermediateRouteBuilder
            };
            Calculator eigenTrustCalculator = new()
            {
                MorphClient = morphClient,
                InitialTrustSource = new()
                {
                    NetmapCache = netmapCache
                },
                IntermediateValueTarget = intermediateTrustRouter,
                WorkerPool = workPool,
                FinalWriteTarget = new()
                {
                    PrivateKey = key,
                    PublicKey = key.PublicKey(),
                    MorphClient = morphClient
                },
                DaughterTrustSource = new()
                {
                    DaughterStorage = daughterStorage,
                    ConsumerStorage = consumersStorage
                }
            };
            Services.Reputaion.EigenTrust.Control.Controller eigenTrustController = new()
            {
                DaughterTrustCalculator = eigenTrustCalculator,
                MorphClient = morphClient,
                WorkerPool = workPool
            };
            Services.Reputaion.Local.Control.Controller localTrustController = new()
            {
                LocalTrustStorage = localTrustStorage,
                Router = localTrustRouter
            };
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is MorphEvent.NewEpochEvent e)
                {
                    localTrustController.Report(e.EpochNumber);
                }
            });
            return new ReputationServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        ReputationService = new()
                        {
                            LocalRouter = localTrustRouter,
                            IntermediateRouter = intermediateTrustRouter,
                            RouteBuilder = localRouteBuilder
                        }
                    }
                }
            };
        }
    }
}