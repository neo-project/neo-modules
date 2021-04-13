using Google.Protobuf;
using Neo.FileStorage.API.Object;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.API.Cryptography;

namespace Neo.FileStorage.Services.Object.Util
{
    public class Responser
    {
        public ECDsa Key { get; init; }

        public HeadResponse HeadResponse(bool minimal, V2Object obj)
        {
            return new HeadResponse
            {
                Body = minimal ? ToShortHeader(obj) : ToFullHeader(obj),
            };
        }

        private HeadResponse.Types.Body ToFullHeader(V2Object obj)
        {
            return new HeadResponse.Types.Body
            {
                Header = new HeaderWithSignature
                {
                    Header = obj.Header,
                    Signature = obj.Signature,
                }
            };
        }

        private HeadResponse.Types.Body ToShortHeader(V2Object obj)
        {
            return new HeadResponse.Types.Body
            {
                ShortHeader = new ShortHeader
                {
                    Version = obj.Header.Version,
                    OwnerId = obj.Header.OwnerId,
                    CreationEpoch = obj.Header.CreationEpoch,
                    ObjectType = obj.Header.ObjectType,
                    PayloadLength = obj.Header.PayloadLength,
                }
            };
        }

        public GetResponse GetInitResponse(V2Object obj)
        {
            var resp = new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Init = new GetResponse.Types.Body.Types.Init
                    {
                        Header = obj.Header,
                        ObjectId = obj.ObjectId,
                        Signature = obj.Signature,
                    }
                }
            };
            Key.SignResponse(resp);
            return resp;
        }

        public GetResponse GetChunkResponse(ByteString chunk)
        {
            var resp = new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Chunk = chunk,
                }
            };
            Key.SignResponse(resp);
            return resp;
        }

        public GetRangeResponse GetRangeResponse(ByteString chunk)
        {
            var resp = new GetRangeResponse
            {
                Body = new GetRangeResponse.Types.Body
                {
                    Chunk = chunk,
                }
            };
            Key.SignResponse(resp);
            return resp;
        }

        public GetRangeHashResponse GetRangeHashResponse(List<byte[]> hashes)
        {
            var body = new GetRangeHashResponse.Types.Body();
            body.HashList.AddRange(hashes.Select(p => ByteString.CopyFrom(p)));
            return new GetRangeHashResponse
            {
                Body = body,
            };
        }
    }
}