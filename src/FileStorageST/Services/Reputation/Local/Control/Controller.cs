using System.Collections.Generic;
using System.Threading;
using Neo.FileStorage.Storage.Services.Reputaion.Common;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Control
{
    public class Controller
    {
        public IIteratorProvider LocalTrustStorage { get; init; }
        public IWriterProvider Router { get; init; }
        public byte[] LocalKey { get; init; }
        private readonly Dictionary<ulong, CancellationTokenSource> sources = new();

        public void Report(ulong epoch)
        {
            var context = AcquireReport(epoch);
            context.Report();
            FreeReport(epoch);
        }

        public void Stop(ulong epoch)
        {
            FreeReport(epoch);
        }

        public ReportContext AcquireReport(ulong epoch)
        {
            if (sources.ContainsKey(epoch))
            {
                return null;
            }
            var source = new CancellationTokenSource();
            return new()
            {
                Cancel = source.Token,
                Epoch = epoch,
                Controller = this,
            };
        }

        public void FreeReport(ulong epoch)
        {
            if (sources.TryGetValue(epoch, out CancellationTokenSource source))
            {
                source.Cancel();
                source.Dispose();
                sources.Remove(epoch);
            }
        }
    }
}
