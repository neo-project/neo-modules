using System.Collections.Generic;
using System.Threading;
using Neo.FSNode.Services.Audit.Auditor;
using Neo.FSNode.Services.Object.Util;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Tombstone;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Delete.Execute
{
    public class Executor
    {
        private ExecuteContext context;
        private CancellationToken Context => context.Context;
        private Address Address => context.Prm.Address;
        private ContainerID ContainerID => context.Prm.Address.ContainerId;
        private bool IsLocal => context.Prm.Local == true;
        private CommonPrm CommonParameters => context.Prm;

        public void Execute()
        {
            if (!ExecuteLocal())
                ExecuteOnContainer();
        }

        private bool ExecuteLocal()
        {
            return true;
        }

        private void AddMembers(List<ObjectID> incoming)
        {
            var members = context.Tombstone.Members;
            foreach (var member in members)
            {
                foreach (var i in incoming)
                {
                    if (i == member)
                    {
                        incoming.Remove(i);
                    }
                }
            }
            context.Tombstone.Members.AddRange(incoming);
        }

        private bool FromSplitInfo()
        {
            splitInfo = DeleteService.GetService.Head();
        }

        private bool FromTombstone()
        {
            context.Tombstone = new Tombstone();
            AddMembers(new List<ObjectID> { Address.ObjectId });
        }

        private void ExecuteOnContainer()
        {
            Utility.Log(nameof(Executor), LogLevel.Info, "request is not rolled over to the container");
        }
    }
}
