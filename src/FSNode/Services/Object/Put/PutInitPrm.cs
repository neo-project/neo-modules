using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object.Object;
using V2Container = NeoFS.API.v2.Container.Container;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;

namespace Neo.FSNode.Services.Object.Put
{
    public class PutInitPrm : CommonPrm
    {
        public V2Object Init;
        public V2Container Container;
        public IBuilder Builder;

        public static PutInitPrm FromRequest(PutRequest request)
        {
            var body = request?.Body;
            if (body is null) throw new InvalidOperationException(nameof(PutInitPrm) + " invalid request");
            if (body.ObjectPartCase != PutRequest.Types.Body.ObjectPartOneofCase.Init)
                throw new InvalidOperationException(nameof(PutInitPrm) + " invalid request");
            var init = body.Init;
            if (init is null)
                throw new InvalidOperationException(nameof(PutInitPrm) + " invalid init request");
            var obj = new V2Object
            {
                ObjectId = init.ObjectId,
                Signature = init.Signature,
                Header = init.Header,
            };
            var prm = new PutInitPrm
            {
                Init = obj,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
