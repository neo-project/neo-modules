using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Storage;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Services.Container.Announcement.Control
{
    public class Controller
    {
        public LocalStorageLoad LocalMetrics;
        public Router LocalAnnouncementTarget;
        public AnnouncementStorage AnnouncementAccumulator;
        public MorphLoadWriter ResultReceiver;
        public Dictionary<ulong, CancellationTokenSource> AnnounceCancellations = new ();
        public Dictionary<ulong, CancellationTokenSource> ReportCancellations = new ();

        public void Start(ulong epoch)
        {
            AnnounceContext context = AcquireAnnouncement(epoch);
            if (context is null) return;
            context.Announce();
            FreeAnnouncement(epoch);
        }

        public AnnounceContext AcquireAnnouncement(ulong epoch)
        {
            CancellationToken cancellation;
            lock (AnnounceCancellations)
            {
                if (!AnnounceCancellations.ContainsKey(epoch))
                {
                    CancellationTokenSource source = new ();
                    AnnounceCancellations[epoch] = source;
                    cancellation = source.Token;
                }
                else
                {
                    return null;
                }
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
            CancellationToken cancellation;
            lock (ReportCancellations)
            {
                if (!ReportCancellations.ContainsKey(epoch))
                {
                    CancellationTokenSource source = new ();
                    ReportCancellations[epoch] = source;
                    cancellation = source.Token;
                }
                else
                {
                    return null;
                }
            }
            return new AnnounceContext
            {
                Epoch = epoch,
                Controller = this,
                Cancellation = cancellation,
            };
        }

        public void Stop(ulong epoch)
        {
            AnnounceContext context = AcquireReport(epoch);
            if (context is null) return;
            FreeAnnouncement(epoch);
            context.Report();
            FreeReport(epoch);
        }

        public void FreeAnnouncement(ulong epoch)
        {
            lock (AnnounceCancellations)
            {
                if (AnnounceCancellations.TryGetValue(epoch, out CancellationTokenSource token))
                {
                    token.Cancel();
                    AnnounceCancellations.Remove(epoch);
                }
            }
        }

        public void FreeReport(ulong epoch)
        {
            lock (ReportCancellations)
            {
                if (ReportCancellations.TryGetValue(epoch, out CancellationTokenSource token))
                {
                    token.Cancel();
                    ReportCancellations.Remove(epoch);
                }
            }
        }
    }
}
