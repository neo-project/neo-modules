using Neo.FileStorage.API.Netmap;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common.Route
{
    public class RouteContext : ICommonContext
    {
        public ICommonContext CommonContext { get; private set; }
        public CancellationToken Cancellation => CommonContext.Cancellation;
        public ulong Epoch => CommonContext.Epoch;
        public List<NodeInfo> Passed { get; private set; }

        public RouteContext(ICommonContext common, List<NodeInfo> passed)
        {
            CommonContext = common;
            Passed = passed;
        }
    }
}
