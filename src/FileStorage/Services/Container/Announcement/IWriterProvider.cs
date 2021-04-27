using System.Threading;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public interface IWriterProvider
    {
        IWriter InitWriter(CancellationToken cancellation);
    }
}
