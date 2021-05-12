using System.Threading;

namespace Neo.FileStorage.Services.Reputaion.EigenTrust.Control
{
    public class IterationContextCancellable
    {
        public IterationContext Context;
        public CancellationTokenSource Cancel;
    }
}
