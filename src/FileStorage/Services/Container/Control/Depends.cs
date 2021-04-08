using System;
using System.Threading;
using static Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types;

namespace Neo.FileStorage.Services.Container.Control
{
    public interface IIteratorProvider
    {
        IIterator InitIterator(CancellationToken cancellation);
    }

    public interface IIterator
    {
        void Iterator(Func<Announcement, bool> filter, Action<Announcement> handler);
    }

    public interface IWriter
    {
        void Put(Announcement announcement);
        void Close();
    }

    public interface IWriterProvider
    {
        IWriter InitWriter(CancellationToken cancellation);
    }
}
