using Neo.FileStorage.Services.Container.Announcement.Route;
using System;
using System.Threading;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Container.Announcement.Control
{
    public class AnnounceContext
    {
        public CancellationToken Cancellation;
        public ulong Epoch;
        public Controller Controller;

        public void Announce()
        {
            try
            {
                LoadWriter targetWriter = Controller.LocalAnnouncementTarget.InitWriter(Cancellation);
                Controller.LocalMetrics.Iterate(a => true, a =>
                {
                    a.Epoch = Epoch;
                    targetWriter.Put(a);
                });
                targetWriter.Close();
            }
            catch (Exception e)
            {
                Log(nameof(AnnounceContext), LogLevel.Debug, $"exception when announce, error={e.Message}");
            }
        }

        public void Report()
        {
            try
            {
                Controller.AnnouncementAccumulator.Iterate(a => a.Epoch == Epoch, Controller.ResultReceiver.Put);
            }
            catch (Exception e)
            {
                Log(nameof(AnnounceContext), LogLevel.Debug, $"exception when report, error={e.Message}");
            }
        }
    }
}
