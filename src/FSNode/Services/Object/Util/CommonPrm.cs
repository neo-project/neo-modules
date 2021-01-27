using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Session;
using NeoFS.API.v2.Client;
using System.Security.Cryptography;
using System.Threading;

namespace Neo.FSNode.Services.Object.Util
{
    public class CommonPrm
    {
        public CancellationToken Context;
        public bool Local;
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

        public void WithCommonPrm(CommonPrm other)
        {
            Context = other.Context;
            Local = other.Local;
            SessionToken = other.SessionToken;
            BearerToken = other.BearerToken;
            Key = other.Key;
            CallOptions = other.CallOptions;
        }
    }
}
