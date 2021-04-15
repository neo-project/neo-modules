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
