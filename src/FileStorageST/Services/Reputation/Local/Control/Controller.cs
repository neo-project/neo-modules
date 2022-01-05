using Neo.FileStorage.Storage.Services.Reputaion.Common;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Control
{
    public class Controller
    {
        public IIteratorProvider LocalTrustStorage { get; init; }
        public IWriterProvider Router { get; init; }
        public byte[] LocalKey { get; init; }
        private readonly Dictionary<ulong, CancellationTokenSource> cancellationSources = new();

        public void Report(ulong epoch)
        {
            Utility.Log(nameof(Controller), LogLevel.Debug, $"reporting local trust, epoch={epoch}");
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
            if (cancellationSources.ContainsKey(epoch))
            {
                return null;
            }
            var cancellationSource = new CancellationTokenSource();
            return new()
            {
                Cancellation = cancellationSource.Token,
                Epoch = epoch,
                Controller = this,
            };
        }

        public void FreeReport(ulong epoch)
        {
            if (cancellationSources.TryGetValue(epoch, out CancellationTokenSource source))
            {
                source.Cancel();
                source.Dispose();
                cancellationSources.Remove(epoch);
            }
        }
    }
}
