using System;
using System.Threading;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Control
{
    public interface IIteratorProvider
    {
        IIterator InitIterator(CancellationToken cancellation);
    }

    public interface IIterator
    {
        void Iterate(Func<FSAnnouncement, bool> filter, Action<FSAnnouncement> handler);
    }

    public interface IWriter
    {
        void Put(FSAnnouncement announcement);
        void Close();
    }

    public interface IWriterProvider
    {
        IWriter InitWriter(CancellationToken cancellation);
    }
}
