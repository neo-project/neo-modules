using Grpc.Core;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Object.Util;
using System.Collections.Generic;
using Neo.FileStorage.Services.Object.Get.Writer;
using V2Object = Neo.FileStorage.API.Object.Object;
using V2Range = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class GetService
    {
        public GetPrm ToGetPrm(GetRequest request)
        {
            var key = KeyStorage.GetKey(request.MetaHeader.SessionToken);
            var prm = GetPrm.FromRequest(request);
            prm.Key = key;
            return prm;
        }
    }
}
