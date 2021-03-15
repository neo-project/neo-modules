using System.Collections.Generic;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Get;
using Neo.FSNode.Services.Object.Get.Writer;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Delete.Execute
{
    public static class Util
    {
        public static V2Object HeadAddress(this GetService service, ExecuteContext context, Address address)
        {
            var writer = new SimpleObjectWriter();
            var prm = new HeadPrm
            {
                Address = address,
                Raw = true,
                HeaderWriter = writer,
                Short = false,
            };
            prm.WithCommonPrm(context.Prm);
            return service.Head(prm);
        }

        public static SplitInfo SplitInfo(this GetService service, ExecuteContext context)
        {
            
        }

        public static List<ObjectID> Children(this GetService service, ExecuteContext context)
        {

        }

        public static ObjectID Previous(this GetService service, ExecuteContext context, ObjectID oid)
        {

        }

        public static List<ObjectID> SplitMembers(this GetService service, ExecuteContext context)
        {

        }

        public static ObjectID Put(this GetService service, ExecuteContext context, bool broadcast)
        {

        }
    }
}
