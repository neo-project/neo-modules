using System.Threading;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network;
using Neo.FileStorage.Services.Object.Put.Writer;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.Reputaion.Local.Client;

namespace Neo.FileStorage.Services.Object.Put
{
    public class PutService
    {
        public Client MorphClient { get; init; }
        public Address LocalAddress { get; init; }
        public KeyStorage KeyStorage { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public LocalObjectInhumer ObjectInhumer { get; init; }
        public ReputationClientCache ClientCache { get; init; }

        public PutInitPrm ToInitPrm(PutRequest request)
        {
            var prm = PutInitPrm.FromRequest(request);
            return prm;
        }

        public PutStream Put(CancellationToken cancellation)
        {
            return new PutStream()
            {
                Cancellation = cancellation,
                PutService = this,
            };
        }
    }
}
