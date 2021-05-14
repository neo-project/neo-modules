using System.Threading;
using Neo.FileStorage.Services.Reputaion.Common;

namespace Neo.FileStorage.Services.Reputaion.Local.Control
{
    public class IterationContext : ICommonContext
    {
        public CancellationToken Cancellation { get; init; }
        public ulong Epoch { get; init; }
    }
}
