using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Placement;
using System;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteContainer()
        {
            InitEpoch();
            var depth = Prm.NetmapLookupDepth;
            while (true)
            {
                if (ProcessCurrentEpoch()) break;
                if (depth == 0) break;
                depth--;
                currentEpoch--;
            };
        }

        private void InitEpoch()
        {
            currentEpoch = Prm.NetmapEpoch;
            if (0 < currentEpoch) return;
            currentEpoch = SearchService.EpochSource.CurrentEpoch;
        }

        private Traverser GenerateTraverser(ContainerID cid)
        {
            return SearchService.TraverserGenerator.GenerateTraverser(new() { ContainerId = cid }, currentEpoch);
        }

        private bool ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.ContainerID);
            while (true)
            {
                var ns = traverser.Next();
                if (ns.Count == 0)
                {
                    Log(nameof(Search.Execute), LogLevel.Debug, "no more nodes, abort placement iteration");
                    return false;
                }
                foreach (var n in ns)
                {
                    if (Cancellation.IsCancellationRequested) throw new OperationCanceledException();
                    ProcessNode(n.Info);
                }
            }
        }
    }
}
