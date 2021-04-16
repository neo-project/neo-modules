using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Client;
using System.Security.Cryptography;

namespace Neo.FileStorage.Services.Object.Util
{
    public class CommonPrm
    {
        public bool Local;
        public ulong NetmapEpoch;
        public ulong NetmapLookupDepth;
        public SessionToken SessionToken;
        public BearerToken BearerToken;
        public ECDsa Key;
        public CallOptions CallOptions;

        public static CommonPrm FromRequest(IRequest request)
        {
            var meta = request.MetaHeader;
            var prm = new CommonPrm
            {
                Local = meta.Ttl <= 1,
                SessionToken = meta.SessionToken,
                BearerToken = meta.BearerToken,
            };
            foreach (var header in meta.XHeaders)
            {
                switch (header.Key)
                {
                    case XHeader.XHeaderNetmapEpoch:
                        prm.NetmapEpoch = uint.Parse(header.Value);
                        break;
                    case XHeader.XHeaderNetmapLookupDepth:
                        prm.NetmapLookupDepth = uint.Parse(header.Value);
                        break;
                    default:
                        //TODO: call options
                        break;
                }
            }
            return prm;
        }

        public CommonPrm WithCommonPrm(CommonPrm other)
        {
            Local = other.Local;
            NetmapEpoch = other.NetmapEpoch;
            NetmapLookupDepth = other.NetmapLookupDepth;
            SessionToken = other.SessionToken;
            BearerToken = other.BearerToken;
            Key = other.Key;
            CallOptions = other.CallOptions;
            return this;
        }
    }
}
