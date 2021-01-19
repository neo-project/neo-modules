using Neo.FSNode.Services.Audit;

namespace Neo.Plugins.Innerring.Processors
{
    public class EpochAuditReporter : IReporter
    {
        public ulong epoch;

        public IReporter reporter;

        public void WriteReport(Report report)
        {
            report.Result().AuditEpoch = epoch;
            reporter.WriteReport(report);
        }
    }
}
