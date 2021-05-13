using System.Threading;
using Neo.FileStorage.Services.Reputaion.Common;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust
{
    public class IterationContext : ICommonContext
    {
        public CancellationToken Cancellation { get; init; }
        public ulong Epoch { get; init; }
        public uint Index;
        public bool Last;
    }
}
