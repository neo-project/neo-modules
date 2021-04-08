
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
                IIterator metricsIterator = Controller.LocalMetrics.InitIterator(Cancellation);
                IWriter targetWriter = Controller.LocalAnnouncementTarget.InitWriter(Cancellation);
                metricsIterator.Iterate(a => true, a =>
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
                IIterator localIterator = Controller.AnnouncementAccumulator.InitIterator(Cancellation);
                IWriter resultWriter = Controller.ResultReceiver.InitWriter(Cancellation);
                localIterator.Iterate(a => a.Epoch == Epoch, resultWriter.Put);
                resultWriter.Close();
            }
            catch (Exception e)
            {
                Log(nameof(AnnounceContext), LogLevel.Debug, $"exception when report, error={e.Message}");
            }
        }
    }
}
