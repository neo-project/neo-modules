using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestPlacementBuilder : IPlacementBuilder
    {
        public Dictionary<Address, List<List<Node>>> Vectors = new();

        public List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy)
        {
            if (Vectors.TryGetValue(address, out var vector))
            {
                return vector;
            }
            throw new InvalidOperationException("vectors for address not found");
        }
    }
}
