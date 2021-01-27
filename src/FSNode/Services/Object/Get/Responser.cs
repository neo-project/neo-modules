using System.Collections.Generic;
using Google.Protobuf;
using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object.Object;
using System.Linq;

namespace Neo.FSNode.Services.Object.Get
{
    public static class Responser
    {
        public static HeadResponse HeadResponse(bool minimal, V2Object obj)
        {
            return new HeadResponse
            {
                Body = minimal ? obj.ToShortHeader() : obj.ToFullHeader(),
            };
        }

        private static HeadResponse.Types.Body ToFullHeader(this V2Object obj)
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

        private static HeadResponse.Types.Body ToShortHeader(this V2Object obj)
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

        public static GetResponse GetInitResponse(V2Object obj)
        {
            return new GetResponse
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
        }

        public static GetResponse GetChunkResponse(ByteString chunk)
        {
            return new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Chunk = chunk,
                }
            };
        }

        public static GetRangeResponse GetRangeResponse(ByteString chunk)
        {
            return new GetRangeResponse
            {
                Body = new GetRangeResponse.Types.Body
                {
                    Chunk = chunk,
                }
            };
        }

        public static GetRangeHashResponse GetRangeHashResponse(List<byte[]> hashes)
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