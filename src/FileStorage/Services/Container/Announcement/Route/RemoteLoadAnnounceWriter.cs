
using Neo.FileStorage.API.Client;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class RemoteLoadAnnounceWriter : IWriter
    {
        public CancellationToken Cancellation { get; init; }
        public Client Client { get; init; }
        public ECDsa Key { get; init; }
        private readonly List<FSAnnouncement> buffer = new();

        public void Put(FSAnnouncement announcement)
        {
            buffer.Add(announcement);
        }

        public void Close()
        {
            Client.AnnounceContainerUsedSpace(buffer, context: Cancellation);
        }
    }
}
