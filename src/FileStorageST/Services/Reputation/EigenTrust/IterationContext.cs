using Neo.FileStorage.Storage.Services.Reputaion.Common;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class IterationContext : ICommonContext
    {
        public CancellationToken Cancellation { get; init; }
        public ulong Epoch { get; init; }
        public uint Index;
        public bool Last;
    }
}
