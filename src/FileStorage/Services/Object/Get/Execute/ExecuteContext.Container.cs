using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Object.Get.Execute
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

        private bool ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.Address);
            while (true)
            {
                var addrs = traverser.Next();
                if (!addrs.Any())
                {
                    Log("GetExecutor", LogLevel.Debug, " no more nodes, abort placement iteration");
                    return false;
                }
                foreach (var addr in addrs)
                {
                    if (ProcessNode(addr))
                    {
                        Log(nameof(ExecuteOnContainer), LogLevel.Debug, " completing the operation");
                        return true;
                    }
                }
            }
        }
    }
}
