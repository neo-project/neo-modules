using Neo.FileStorage.API.Client;
using System.Security.Cryptography;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceWriterProvider : IWriterProvider
    {
        public ECDsa Key { get; init; }
        public IFSClient Client;

        public IWriter InitWriter(CancellationToken cancellation)
        {
            return new RemoteLoadAnnounceWriter
            {
                Cancellation = cancellation,
                Key = Key,
                Client = Client,
            };
        }
    }
}
