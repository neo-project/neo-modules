using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Session;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.FSNode.Services.Object.Util
{
    public class CommonPrm
    {
        public bool Local;
        public SessionToken SessionToken;
        public BearerToken BearerToken;

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

        public void WithCommonPrm(CommonPrm cprm)
        {
            Local = cprm.Local;
            SessionToken = cprm.SessionToken;
            BearerToken = cprm.BearerToken;
        }
    }
}
