using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using System;
using System.Threading;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Control
{
    public class AnnounceContext
    {
        public CancellationToken Cancellation { get; init; }
        public ulong Epoch { get; init; }
        public Controller Controller { get; init; }

        public void Announce()
        {
            try
            {
                IIterator metricsIterator = Controller.LocalMetrics.InitIterator(Cancellation);
                IWriter targetWriter = Controller.LocalAnnouncementTarget.InitWriter(Cancellation);
                metricsIterator.Iterate(a => true, a =>
                {
                    a.Epoch = Epoch;
                    try
                    {
                        targetWriter.Put(a);
                    }
                    catch (Exception e)
                    {
                        Log(nameof(AnnounceContext), LogLevel.Warning, $"couldn't put announcement to local target, cid={a.ContainerId.String()}, used_space={a.UsedSpace}, error={e.Message}");
                    }
                });
                targetWriter.Close();
            }
            catch (Exception e)
            {
                Log(nameof(AnnounceContext), LogLevel.Warning, $"exception when announce, error={e.Message}");
            }
        }

        public void Report()
        {
            try
            {
                IIterator localIterator = Controller.AnnouncementAccumulator.InitIterator(Cancellation);
                IWriter resultWriter = Controller.ResultReceiver.InitWriter(Cancellation);
                localIterator.Iterate(a => a.Epoch == Epoch, resultWriter.Put);
            }
            catch (Exception e)
            {
                Log(nameof(AnnounceContext), LogLevel.Warning, $"exception when report, error={e.Message}");
            }
        }
    }
}
