using System;
using System.Collections.Generic;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Storage
{
    public class AnnouncementStorage : IWriter
    {
        private class AnnounceUsedSpaceEstimation
        {
            public FSAnnouncement Announcement;
            public List<ulong> Sizes;
        }

        private readonly Dictionary<string, AnnounceUsedSpaceEstimation> mItems = new();

        public void Put(FSAnnouncement announcement)
        {
            lock (mItems)
            {
                var key = announcement.Epoch.ToString() + announcement.ContainerId.String();
                bool exists = mItems.TryGetValue(key, out AnnounceUsedSpaceEstimation estimation);
                if (!exists)
                {
                    estimation = new()
                    {
                        Announcement = new()
                        {
                            Epoch = announcement.Epoch,
                            ContainerId = announcement.ContainerId,
                            UsedSpace = 0,
                        },
                        Sizes = new(),
                    };
                    mItems[key] = estimation;
                }
                estimation.Sizes.Add(announcement.UsedSpace);
            }
        }

        public void Close() { }

        public void Iterate(Func<FSAnnouncement, bool> filter, Action<FSAnnouncement> handler)
        {
            foreach (var (_, estimation) in mItems)
            {
                if (estimation.Announcement is not null && filter(estimation.Announcement))
                {
                    estimation.Announcement.UsedSpace = Helper.FinalEstimation(estimation.Sizes);
                    handler(estimation.Announcement);
                }
            }
        }
    }
}
