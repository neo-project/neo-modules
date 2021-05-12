using System.Threading;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Control
{
    public class IterationContext
    {
        public CancellationToken Cancellation { get; init; }
        public ulong Epoch { get; init; }
        public uint Index;
        public bool Last;
    }
}
