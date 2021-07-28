using System.Collections.Generic;
using System.Threading;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Control
{
    public class Controller
    {
        public IIteratorProvider LocalMetrics { get; init; }
        public IWriterProvider LocalAnnouncementTarget { get; init; }
        public IIteratorProvider AnnouncementAccumulator { get; init; }
        public IWriterProvider ResultReceiver { get; init; }
        private readonly Dictionary<ulong, CancellationTokenSource> announceSources = new();
        private readonly Dictionary<ulong, CancellationTokenSource> reportSources = new();

        public void Start(ulong epoch)
        {
            AnnounceContext context = AcquireAnnouncement(epoch);
            if (context is null) return;
            context.Announce();
            FreeAnnouncement(epoch);
        }

        public void Stop(ulong epoch)
        {
            AnnounceContext context = AcquireReport(epoch);
            if (context is null) return;
            FreeAnnouncement(epoch);
            context.Report();
            FreeReport(epoch);
        }

        public AnnounceContext AcquireAnnouncement(ulong epoch)
        {
            CancellationToken token;
            lock (announceSources)
            {
                if (announceSources.ContainsKey(epoch))
                {
                    return null;
                }
                CancellationTokenSource source = new();
                announceSources[epoch] = source;
                token = source.Token;
            }
            return new AnnounceContext
            {
                Epoch = epoch,
                Controller = this,
                Cancellation = token,
            };
        }

        public AnnounceContext AcquireReport(ulong epoch)
        {
            CancellationToken token;
            lock (reportSources)
            {
                if (reportSources.ContainsKey(epoch))
                {
                    return null;
                }
                CancellationTokenSource source = new();
                reportSources[epoch] = source;
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
            lock (announceSources)
            {
                if (announceSources.TryGetValue(epoch, out CancellationTokenSource source))
                {
                    source.Cancel();
                    source.Dispose();
                    announceSources.Remove(epoch);
                }
            }
        }

        public void FreeReport(ulong epoch)
        {
            lock (reportSources)
            {
                if (reportSources.TryGetValue(epoch, out CancellationTokenSource source))
                {
                    source.Cancel();
                    source.Dispose();
                    reportSources.Remove(epoch);
                }
            }
        }
    }
}
