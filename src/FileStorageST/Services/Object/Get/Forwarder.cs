using Google.Protobuf;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class Forwarder
    {
        private readonly CancellationToken cancellation;
        private readonly KeyStore keyStore;
        private readonly IRequest request;

        public Forwarder(KeyStore ks, IRequest req, CancellationToken cancel)
        {
            keyStore = ks;
            request = req;
            RequestMetaHeader meta = new();
            meta.Ttl = req.MetaHeader.Ttl - 1;
            meta.Origin = req.MetaHeader;
            request.MetaHeader = meta;
            cancellation = cancel;
        }

        public FSObject Forward(IRawObjectGetClient client)
        {
            var key = keyStore.GetKey(null);
            key.Sign(request);
            switch (request)
            {
                case GetRequest getRequest:
                    return client.GetObject(getRequest).Result;
                case HeadRequest headRequest:
                    return client.GetObjectHeader(headRequest).Result;
                case GetRangeRequest getRangeRequest:
                    var data = client.GetObjectPayloadRangeData(getRangeRequest).Result;
                    return new()
                    {
                        Payload = ByteString.CopyFrom(data)
                    };
                default:
                    throw new InvalidOperationException($"{nameof(Forwarder)} request type not supported");
            }
        }
    }
}
