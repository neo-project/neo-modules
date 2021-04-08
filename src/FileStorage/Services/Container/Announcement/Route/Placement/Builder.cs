using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route.Placement
{
    public class Builder : IBuilder
    {
        public IPlacementBuilder PlacementBuilder;
        public List<NodeInfo> NextStage(FSAnnouncement announcement, List<NodeInfo> passed)
        {
            if (1 < passed.Count) return null;
            var placement = PlacementBuilder.BuildPlacement(announcement.Epoch, announcement.ContainerId);
            return placement.Select(p => p.Any() ? p.First() : null).Where(p => p is not null).ToList();
        }
    }
}
