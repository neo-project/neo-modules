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
            var containers = engine.ListContainers();
            foreach (var cid in containers)
            {
                var size = engine.ContainerSize(cid);
                var a = new FSAnnouncement
                {
                    ContainerId = cid,
                    UsedSpace = size,
                };
                if (filter is not null && !filter(a)) continue;
                handler(a);
            }
        }
    }
}
