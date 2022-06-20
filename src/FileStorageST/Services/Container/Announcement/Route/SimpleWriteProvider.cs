using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Container.Announcement
{
    public class SimpleWriteProvider : IWriterProvider
    {
        private readonly IWriter writer;

        public SimpleWriteProvider(IWriter w)
        {
            writer = w;
        }

        public IWriter InitWriter(CancellationToken cancellation)
        {
            if (writer is null) throw new InvalidOperationException($"{nameof(SimpleWriteProvider)} no writer");
            return writer;
        }
    }
}
