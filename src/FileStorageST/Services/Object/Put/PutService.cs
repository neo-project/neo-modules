using Akka.Actor;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Object.Util;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public class PutService
    {
        public const int DefaultRemotePoolSize = 100;
        public const int DefaultLocalPoolSize = 101;

        public IMaxObjectSizeSource MaxObjectSizeSource { get; init; }
        public IContainerSource ContainerSoruce { get; init; }
        public INetmapSource NetmapSource { get; init; }
        public IEpochSource EpochSource { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public KeyStore KeyStorage { get; init; }
        public ILocalObjectStore LocalObjectStore { get; init; }
        public IObjectInhumer ObjectInhumer { get; init; }
        public IPutClientCache ClientCache { get; init; }
        public IActorRef RemotePool { get; init; }
        public IActorRef LocalPool { get; init; }

        public PutStream Put(CancellationToken cancellation)
        {
            return new PutStream()
            {
                InnerStream = new()
                {
                    Cancellation = cancellation,
                    PutService = this,
                },
                PutService = this,
            };
        }
    }
}
