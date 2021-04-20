using Neo.FileStorage.API.Object;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Network;
using Neo.FileStorage.Services.Object.Put.Writer;
using Neo.FileStorage.Services.Object.Util;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Put
{
    public class PutService
    {
        public Client MorphClient { get; init; }
        public Address LocalAddress { get; init; }
        public KeyStorage KeyStorage { get; init; }
        public LocalObjectInhumer ObjectInhumer { get; init; }

        public PutInitPrm ToInitPrm(PutRequest request)
        {
            return PutInitPrm.FromRequest(request);
        }

        public IPutRequestStream Put(CancellationToken cancellation)
        {
            return new PutStream()
            {
                PutService = this,
            };
        }
    }
}
