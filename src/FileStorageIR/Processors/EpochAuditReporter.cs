using Neo.FileStorage.InnerRing.Services.Audit;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class EpochAuditReporter : IReporter
    {
        public ulong Epoch;
        public IReporter Reporter;

        public void WriteReport(Report report)
        {
            report.SetEpoch(Epoch);
            Reporter.WriteReport(report);
        }
    }
}
