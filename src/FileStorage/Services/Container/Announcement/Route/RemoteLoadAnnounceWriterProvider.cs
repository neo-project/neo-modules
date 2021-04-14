using Neo.FileStorage.API.Client;
using System;
using System.Security.Cryptography;
using System.Threading;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceWriterProvider
    {
        private readonly ECDsa key;
        private readonly Client client;

        public RemoteAnnounceWriter InitWriter(CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }
    }
}
