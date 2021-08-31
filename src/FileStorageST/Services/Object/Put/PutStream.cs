using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public sealed class PutStream : IRequestStream
    {
        public PutService PutService { get; init; }
        public InnerStream InnerStream { get; init; }
        private PutRequest init;
        private readonly List<PutRequest> chunks = new();
        private bool saveChunks;
        private ulong writenPayloadSize;
        private ulong payloadSize;

        public PutInitPrm ToInitPrm(PutRequest request)
        {
            var prm = PutInitPrm.FromRequest(request);
            prm.Relay = RelayRequest;
            return prm;
        }

        public void Send(IRequest request)
        {
            if (request is not PutRequest putRequest)
                throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            switch (putRequest.Body.ObjectPartCase)
            {
                case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                    var init_prm = ToInitPrm(putRequest);
                    InnerStream.Init(init_prm);
                    saveChunks = putRequest.Body.Init.Signature is not null;
                    if (saveChunks)
                    {
                        payloadSize = putRequest.Body.Init.Header.PayloadLength;
                        if (InnerStream.MaxObjectSize < payloadSize)
                            throw new InvalidOperationException("payload size is greater than the limit");
                        init = putRequest;
                    }
                    break;
                case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                    if (saveChunks)
                    {
                        writenPayloadSize += (ulong)putRequest.Body.Chunk.Length;
                        if (payloadSize < writenPayloadSize)
                            throw new InvalidOperationException("wrong payload size");
                    }
                    InnerStream.Chunk(putRequest.Body.Chunk);
                    if (saveChunks)
                        chunks.Add(putRequest);
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            }
            if (!saveChunks) return;
            var metaHdr = new RequestMetaHeader();
            var meta = request.MetaHeader;
            metaHdr.Ttl = meta.Ttl - 1;
            metaHdr.Origin = meta;
            request.MetaHeader = metaHdr;
            var key = PutService.KeyStorage.GetKey(meta.SessionToken);
            key.SignRequest(request);
        }

        public IResponse Close()
        {
            var ids = InnerStream.Close();
            return new PutResponse
            {
                Body = new PutResponse.Types.Body
                {
                    ObjectId = ids.Parent ?? ids.Self,
                }
            };
        }

        public void Dispose()
        {
            InnerStream?.Dispose();
        }

        public async Task<bool> RelayRequest(IPutClient client)
        {
            if (!saveChunks) return false;
            using var stream = await client.PutObject(init, context: InnerStream.Token);
            foreach (var chunk in chunks)
                await stream.Write(chunk);
            await stream.Close();
            return true;
        }
    }
}
