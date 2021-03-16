using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Tombstone;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Delete.Execute
{
    public class Executor
    {
        public ExecuteContext Context;
        private Address Address => Context.Prm.Address;
        private ContainerID ContainerID => Context.Prm.Address.ContainerId;
        private bool IsLocal => Context.Prm.Local == true;
        private CommonPrm CommonParameters => Context.Prm;

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
            var members = Context.Tombstone.Members;
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
            Context.Tombstone.Members.AddRange(incoming);
        }

        private bool FromSplitInfo()
        {
            throw new NotImplementedException();
        }

        private bool FromTombstone()
        {
            Context.Tombstone = new Tombstone();
            AddMembers(new List<ObjectID> { Address.ObjectId });
            return true;
        }

        private void ExecuteOnContainer()
        {
            Utility.Log(nameof(Executor), LogLevel.Info, "request is not rolled over to the container");
        }
    }
}
