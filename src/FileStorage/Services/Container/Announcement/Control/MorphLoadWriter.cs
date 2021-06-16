using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Container.Announcement.Route;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Control
{
    public class MorphLoadWriter : IWriter
    {
        public Client MorphClient { get; init; }
        public byte[] PublicKey { get; init; }

        public void Put(FSAnnouncement announcement)
        {
            MorphClient.AnnounceLoad(announcement, PublicKey);
        }

        public void Close() { }
    }
}
