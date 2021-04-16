using Neo.FileStorage.API.Object;
using FSObject = Neo.FileStorage.API.Object.Object;
using V2Container = Neo.FileStorage.API.Container.Container;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;

namespace Neo.FileStorage.Services.Object.Put
{
    public class PutInitPrm : CommonPrm
    {
        public FSObject Init;
        public V2Container Container;
        public IPlacementBuilder Builder;

        public static PutInitPrm FromRequest(PutRequest request)
        {
            var body = request?.Body;
            if (body is null) throw new InvalidOperationException(nameof(PutInitPrm) + " invalid request");
            if (body.ObjectPartCase != PutRequest.Types.Body.ObjectPartOneofCase.Init)
                throw new InvalidOperationException(nameof(PutInitPrm) + " invalid request");
            var init = body.Init;
            if (init is null)
                throw new InvalidOperationException(nameof(PutInitPrm) + " invalid init request");
            var obj = new FSObject
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
