
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Container.Announcement.Route.Placement
{
    public interface IPlacementBuilder
    {
        List<List<NodeInfo>> BuildPlacement(ulong epoch, ContainerID cid);
    }
}
