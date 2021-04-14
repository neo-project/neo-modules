using System.Collections.Generic;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Storage
{
    public class AnnounceUsedSpaceEstimation
    {
        public FSAnnouncement Announcement;
        public List<ulong> Sizes;
    }
}
