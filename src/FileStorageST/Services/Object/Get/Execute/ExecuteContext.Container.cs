using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Placement;
using static Neo.FileStorage.Network.Helper;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteOnContainer()
        {
            InitEpoch();
            var depth = Prm.NetmapLookupDepth;
            bool result;
            while (true)
            {
                result = ProcessCurrentEpoch();
                if (result) break;
                if (depth == 0) break;
                depth--;
                CurrentEpoch--;
            }
            if (!result)
            {
                if (lastException is not null) throw lastException;
                throw new ObjectNotFoundException();
            }
        }

        private void InitEpoch()
        {
            CurrentEpoch = Prm.NetmapEpoch;
            if (0 < CurrentEpoch) return;
            CurrentEpoch = GetService.EpochSource.CurrentEpoch;
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
                var ns = traverser.Next();
                if (ns.Count == 0)
                {
                    Log(nameof(Get.Execute), LogLevel.Debug, " no more nodes, abort placement iteration");
                    return false;
                }
                foreach (var n in ns)
                {
                    Cancellation.ThrowIfCancellationRequested();
                    if (ProcessNode(n.Info))
                    {
                        Log(nameof(Get.Execute), LogLevel.Debug, "completing the operation");
                        return true;
                    }
                }
            }
        }
    }
}
