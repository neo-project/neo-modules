using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Control
{
    public class MorphLoadWriter : IWriter
    {
        public MorphInvoker MorphInvoker { get; init; }
        public byte[] PublicKey { get; init; }

        public void Put(FSAnnouncement announcement)
        {
            MorphInvoker.AnnounceLoad(announcement, PublicKey);
        }

        public void Close() { }
    }
}
