using Neo.FileStorage.InnerRing.Services.Audit;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class EpochAuditReporter : IReporter
    {
        public ulong epoch;
        public IReporter reporter;

        public void WriteReport(Report report)
        {
            report.SetEpoch(epoch);
            reporter.WriteReport(report);
        }
    }
}
