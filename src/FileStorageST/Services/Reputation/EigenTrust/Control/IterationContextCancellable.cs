using System;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Control
{
    public class IterationContextCancellable : IDisposable
    {
        public IterationContext Context;
        public CancellationTokenSource CancellationSource;

        public void Dispose()
        {
            CancellationSource.Dispose();
        }
    }
}
