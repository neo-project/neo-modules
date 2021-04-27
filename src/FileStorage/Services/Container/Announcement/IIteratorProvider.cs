using System.Threading;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public interface IIteratorProvider
    {
        IIterator InitIterator(CancellationToken cancellation);
    }
}
