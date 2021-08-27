using System.Threading;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Core;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Object.Util;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public class PutService
    {
        public IMaxObjectSizeSource MaxObjectSizeSource { get; init; }
        public IContainerSoruce ContainerSoruce { get; init; }
        public INetmapSource NetmapSource { get; init; }
        public IEpochSource EpochSource { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }
        public KeyStore KeyStorage { get; init; }
        public ILocalObjectStore LocalObjectStore { get; init; }
        public IObjectDeleteHandler ObjectInhumer { get; init; }
        public IPutClientCache ClientCache { get; init; }

        public PutStream Put(CancellationToken token)
        {
            return new PutStream()
            {
                Stream = new()
                {
                    Token = token,
                    PutService = this,
                },
                PutService = this,
            };
        }
    }
}
