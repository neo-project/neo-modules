using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Services.Reputaion.Local.Client;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Search.Execute
{
    public static class Helper
    {
        public static IEnumerable<ObjectID> Search(this StorageEngine engine, ExecuteContext context)
        {
            var r = engine.Select(context.Prm.ContainerID, context.Prm.Filters);
            return r.Select(p => p.ObjectId);
        }

        public static IEnumerable<ObjectID> SearchObjects(this ReputationClient client, ExecuteContext context)
        {
            return client.SearchObject(new()
            {
                ContainerID = context.Prm.ContainerID,
                Filters = context.Prm.Filters
            }, new()
            {
                Epoch = context.Prm.NetmapEpoch,
                Key = context.Prm.Key,
            }).Result;
        }
    }
}
