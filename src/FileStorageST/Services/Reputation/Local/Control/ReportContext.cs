using System;
using System.Threading;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Reputaion.Local.Control
{
    public class ReportContext
    {
        public CancellationToken Cancellation;
        public ulong Epoch;
        public Controller Controller;

        public void Report()
        {
            var ctx = new IterationContext
            {
                Cancellation = Cancellation,
                Epoch = Epoch,
            };
            try
            {
                var iterator = Controller.LocalTrustStorage.InitIterator(ctx);
                var writer = Controller.Router.InitWriter(ctx);
                iterator.Iterate(t =>
                {
                    Cancellation.ThrowIfCancellationRequested();
                    writer.Write(t);
                });
                writer.Close();
            }
            catch (Exception e)
            {
                Log(nameof(ReportContext), LogLevel.Warning, $"report local trust failed, error={e.Message}");
                return;
            }
        }
    }
}
