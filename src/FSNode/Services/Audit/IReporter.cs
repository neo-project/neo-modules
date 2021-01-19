
namespace Neo.FSNode.Services.Audit
{
    public interface IReporter
    {
        void WriteReport(Report r);
    }
}
