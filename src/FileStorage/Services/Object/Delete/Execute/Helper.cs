using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.Object.Search.Writer;
using System;
using System.Collections.Generic;
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
            try
            {
                service.HeadAddress(context, context.Prm.Address);
                return null;
            }
            catch (Exception e) when (e is LocalObjectStorage.SplitInfoException se)
            {
                return se.SplitInfo;
            }
            catch (Exception e) when (e is API.Object.Exceptions.SplitInfoException se)
            {
                return se.SplitInfo();
            }
        }

        public static List<ObjectID> Children(this GetService service, ExecuteContext context)
        {
            Address address = new()
            {
                ObjectId = context.SplitInfo.Link,
                ContainerId = context.Prm.Address.ContainerId,
            };
            var header = service.HeadAddress(context, address);
            return header.Children;
        }

        public static ObjectID Previous(this GetService service, ExecuteContext context, ObjectID oid)
        {
            Address address = new()
            {
                ObjectId = oid,
                ContainerId = context.Prm.Address.ContainerId,
            };
            var header = service.HeadAddress(context, address);
            return header.PreviousId;
        }

        public static List<ObjectID> SplitMembers(this SearchService service, ExecuteContext context)
        {
            SearchFilters filters = new();
            filters.AddSplitIDFilter(MatchType.StringEqual, new SplitID(context.SplitInfo.SplitId));
            SimpleIDWriter writer = new();
            SearchPrm prm = new()
            {
                ContainerID = context.Prm.Address.ContainerId,
                Filters = filters,
                Writer = writer,
            };
            prm.WithCommonPrm(context.Prm);
            service.Search(prm);
            return writer.IDs;
        }

        public static ObjectID Put(this PutService service, ExecuteContext context, bool broadcast)
        {
            throw new NotImplementedException();
        }
    }
}
