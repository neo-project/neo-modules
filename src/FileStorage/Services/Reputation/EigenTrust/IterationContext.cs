using System.Threading;
using Neo.FileStorage.Services.Reputaion.Common;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust
{
    public class IterationContext : ICommonContext
    {
        public CancellationToken Cancellation { get; set; }
        public ulong Epoch { get; set; }
        public uint Index { get; set; }
    }
}
