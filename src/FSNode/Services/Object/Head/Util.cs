using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object;

namespace Neo.FSNode.Services.Object.Head
{
    public static class Util
    {
        public static HeadResponse ToHeadResponse(this V2Object.Object obj, bool minimal)
        {
            return new HeadResponse
            {
                Body = minimal ? obj.ToShortHeader() : obj.ToFullHeader(),
            };
        }

        private static HeadResponse.Types.Body ToFullHeader(this V2Object.Object obj)
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

        private static HeadResponse.Types.Body ToShortHeader(this V2Object.Object obj)
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
    }
}
