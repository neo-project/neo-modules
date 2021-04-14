
using Neo.FileStorage.API.Client;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class RemoteAnnounceWriter
    {
        private readonly CancellationToken cancellation;
        private readonly Client client;
        private readonly ECDsa key;
        private List<FSAnnouncement> buffer = new();

        public void Put(FSAnnouncement announcement)
        {
            buffer.Add(announcement);
        }

        public void Close()
        {
            client.AnnounceContainerUsedSpace(buffer, context: cancellation);
        }
    }
}
