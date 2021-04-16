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
                ProcessCurrentEpoch();
                depth--;
                CurrentEpoch--;
            }
        }

        private void ProcessCurrentEpoch()
        {
            traverser = GenerateTraverser(Prm.Address);
            while (true)
            {
                var addrs = traverser.Next();
                if (!addrs.Any())
                {
                    Log(nameof(ExecuteContext), LogLevel.Debug, " no more nodes, abort placement iteration");
                    break;
                }
                foreach (var addr in addrs)
                {
                    if (ProcessNode(addr))
                    {
                        Log(nameof(ExecuteOnContainer), LogLevel.Debug, " completing the operation");
                        break;
                    }
                }
            }
        }
    }
}
