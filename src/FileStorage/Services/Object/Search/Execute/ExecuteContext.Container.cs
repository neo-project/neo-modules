using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Object.Search.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteContainer()
        {
            InitEpoch();
            var depth = Prm.NetmapLookupDepth;
            while (0 < depth)
            {
                if (ProcessCurrentEpoch()) break;
                depth--;
                currentEpoch--;
            }
        }

        private void InitEpoch()
        {
            currentEpoch = Prm.NetmapEpoch;
            if (0 < currentEpoch) return;
            currentEpoch = MorphContractInvoker.InvokeEpoch(SearchService.MorphClient);
        }

        private Traverser GenerateTraverser(ContainerID cid)
        {
            return SearchService.TraverserGenerator.GenerateTraverser(new() { ContainerId = cid });
        }

        private bool ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.ContainerID);
            while (true)
            {
                var addrs = traverser.Next();
                if (!addrs.Any())
                {
                    Log("GetExecutor", LogLevel.Debug, " no more nodes, abort placement iteration");
                    return false;
                }
                foreach (var addr in addrs)
                    ProcessNode(addr);
            }
        }
    }
}
