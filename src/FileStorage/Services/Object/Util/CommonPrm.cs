using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Client;
using System.Security.Cryptography;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Util
{
    public class CommonPrm
    {
        public CancellationToken Context;
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
            return new CommonPrm
            {
                Local = meta.Ttl <= 1,
                SessionToken = meta.SessionToken,
                BearerToken = meta.BearerToken,
            };
        }

        public CommonPrm WithCommonPrm(CommonPrm other)
        {
            Context = other.Context;
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
