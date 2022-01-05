using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Control
{
    public class Controller
    {
        public IIteratorProvider LocalMetrics { get; init; }
        public IWriterProvider LocalAnnouncementTarget { get; init; }
        public IIteratorProvider AnnouncementAccumulator { get; init; }
        public IWriterProvider ResultReceiver { get; init; }
        private readonly Dictionary<ulong, CancellationTokenSource> announceCancellationSources = new();
        private readonly Dictionary<ulong, CancellationTokenSource> reportCancellationSources = new();

        public void Start(ulong epoch)
        {
            Utility.Log(nameof(Controller), LogLevel.Debug, $"announcing used space, epoch={epoch}");
            AnnounceContext context = AcquireAnnouncement(epoch);
            if (context is null) return;
            context.Announce();
            FreeAnnouncement(epoch);
        }

        public void Stop(ulong epoch)
        {
            Utility.Log(nameof(Controller), LogLevel.Debug, $"report used space, epoch={epoch}");
            AnnounceContext context = AcquireReport(epoch);
            if (context is null) return;
            FreeAnnouncement(epoch);
            context.Report();
            FreeReport(epoch);
        }

        public AnnounceContext AcquireAnnouncement(ulong epoch)
        {
            CancellationToken cancellation;
            lock (announceCancellationSources)
            {
                if (announceCancellationSources.ContainsKey(epoch))
                {
                    return null;
                }
                CancellationTokenSource source = new();
                announceCancellationSources[epoch] = source;
                cancellation = source.Token;
            }
            return new AnnounceContext
            {
                Epoch = epoch,
                Controller = this,
                Cancellation = cancellation,
            };
        }

        public AnnounceContext AcquireReport(ulong epoch)
        {
            CancellationToken token;
            lock (reportCancellationSources)
            {
                if (reportCancellationSources.ContainsKey(epoch))
                {
                    return null;
                }
                CancellationTokenSource source = new();
                reportCancellationSources[epoch] = source;
                token = source.Token;
            }
            return new AnnounceContext
            {
                Epoch = epoch,
                Controller = this,
                Cancellation = token,
            };
        }

        public void FreeAnnouncement(ulong epoch)
        {
            lock (announceCancellationSources)
            {
                if (announceCancellationSources.TryGetValue(epoch, out CancellationTokenSource source))
                {
                    source.Cancel();
                    source.Dispose();
                    announceCancellationSources.Remove(epoch);
                }
            }
        }

        public void FreeReport(ulong epoch)
        {
            lock (reportCancellationSources)
            {
                if (reportCancellationSources.TryGetValue(epoch, out CancellationTokenSource source))
                {
                    source.Cancel();
                    source.Dispose();
                    reportCancellationSources.Remove(epoch);
                }
            }
        }
    }
}
