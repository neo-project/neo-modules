using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Services.Reputaion.Local.Client;

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
            if (context.Prm.Forwarder is not null)
            {
                return context.Prm.Forwarder(client.FSClient);
            }
            return client.SearchObject(
                context.Prm.ContainerID,
                context.Prm.Filters,
                new()
                {
                    Epoch = context.Prm.NetmapEpoch,
                    Key = context.Prm.Key,
                }).Result;
        }
    }
}
