
namespace Neo.FileStorage.Services.Audit
{
    public interface IReporter
    {
        void WriteReport(Report r);
    }
}
