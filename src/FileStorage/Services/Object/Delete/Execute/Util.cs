using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Get.Writer;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Delete.Execute
{
    public static class Util
    {
        public static FSObject HeadAddress(this GetService service, ExecuteContext context, Address address)
        {
            var writer = new SimpleObjectWriter();
            var prm = new HeadPrm
            {
                Address = address,
                Raw = true,
                Writer = writer,
                Short = false,
            };
            prm.WithCommonPrm(context.Prm);
            service.Head(prm);
            return writer.Obj;
        }

        public static SplitInfo SplitInfo(this GetService service, ExecuteContext context)
        {
            throw new NotImplementedException();
        }

        public static List<ObjectID> Children(this GetService service, ExecuteContext context)
        {
            throw new NotImplementedException();
        }

        public static ObjectID Previous(this GetService service, ExecuteContext context, ObjectID oid)
        {
            throw new NotImplementedException();
        }

        public static List<ObjectID> SplitMembers(this GetService service, ExecuteContext context)
        {
            throw new NotImplementedException();
        }

        public static ObjectID Put(this GetService service, ExecuteContext context, bool broadcast)
        {
            throw new NotImplementedException();
        }
    }
}
