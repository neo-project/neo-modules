using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.ObjectManager.Placement
{
    public interface IBuilder
    {
        List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy);
    }
}
