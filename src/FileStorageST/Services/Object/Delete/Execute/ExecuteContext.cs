using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Tombstone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Delete.Execute
{
    public partial class ExecuteContext
    {
        public CancellationToken Cancellation { get; init; }
        public DeleteService DeleteService { get; init; }
        public DeletePrm Prm { get; init; }
        public SplitInfo SplitInfo { get; private set; }
        public FSObject TombstoneObject { get; private set; }

        private Tombstone tombstone;
        private ulong TombstoneLifeTime => DeleteService.TombstoneLifetime;

        public void Execute()
        {
            try
            {
                ExecuteLocal();
            }
            catch (Exception e)
            {
                Utility.Log(nameof(Delete.Execute), LogLevel.Debug, $"local delete failed, error={e.Message}");
            }
        }

        private void AddMembers(List<ObjectID> incoming)
        {
            tombstone.Members.AddRange(incoming.Where(p => !tombstone.Members.Contains(p)));
        }

        private void FromSplitInfo()
        {
            SplitInfo = DeleteService.GetService.SplitInfo(this);
        }

        private void CollectMembers()
        {
            if (SplitInfo is null) return;
            if (SplitInfo.Link is not null)
            {
                try
                {
                    CollectChildren();
                }
                catch (Exception)
                {
                    if (SplitInfo.LastPart is not null)
                    {
                        CollectChain();
                    }
                }

            }
            SupplementBySplitID();
        }

        private void CollectChildren()
        {
            var children = DeleteService.GetService.Children(this);
            AddMembers(children);
        }

        private void CollectChain()
        {
            List<ObjectID> chain = new();
            for (var prev = SplitInfo.LastPart; prev is not null;)
            {
                chain.Add(prev);
                prev = DeleteService.GetService.Previous(this, prev);
            }
            AddMembers(chain);
        }

        private void SupplementBySplitID()
        {
            var chain = DeleteService.SearchService.SplitMembers(this);
            AddMembers(chain);
        }
    }
}
