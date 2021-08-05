using System;
using System.Linq;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Placement;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteOnContainer()
        {
            InitEpoch();
            var depth = Prm.NetmapLookupDepth;
            while (0 < depth)
            {
                if (ProcessCurrentEpoch()) break;
                depth--;
                CurrentEpoch--;
            }
        }

        private void InitEpoch()
        {
            CurrentEpoch = Prm.NetmapEpoch;
            if (0 < CurrentEpoch) return;
            CurrentEpoch = GetService.MorphInvoker.Epoch();
        }

        private Traverser GenerateTraverser(Address address)
        {
            return GetService.TraverserGenerator.GenerateTraverser(address, CurrentEpoch);
        }

        private bool ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.Address);
            while (true)
            {
                var addrses = traverser.Next();
                if (!addrses.Any())
                {
                    Log("GetExecutor", LogLevel.Debug, " no more nodes, abort placement iteration");
                    return false;
                }
                foreach (var addrs in addrses)
                {
                    if (Cancellation.IsCancellationRequested) throw new OperationCanceledException();
                    if (ProcessNode(addrs))
                    {
                        Log(nameof(ExecuteOnContainer), LogLevel.Debug, " completing the operation");
                        return true;
                    }
                }
            }
        }
    }
}
