using Neo.FileStorage.LocalObjectStorage.Engine;
using System;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Control
{
    public class LocalStorageLoad
    {
        private readonly StorageEngine engine;

        public void Iterate(Func<FSAnnouncement, bool> filter, Action<FSAnnouncement> handler)
        {
            throw new NotImplementedException();
        }
    }
}
