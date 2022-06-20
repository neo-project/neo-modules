using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Services.Object.Util;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object
{
    [TestClass]
    public class UT_LocalPlacementBuilder
    {
        private class TestBuilder : IPlacementBuilder
        {
            public Dictionary<Address, List<List<Node>>> NSS = new();
            public List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy)
            {
                if (NSS.TryGetValue(address, out var nss))
                    return nss;
                throw new InvalidOperationException();
            }
        }

        [TestMethod]
        public void TestBuildePlacement()
        {
            string localAddrs = "/ip4/0.0.0.0/tcp/8080";
            TestBuilder b = new();
            TestLocalInfo localInfo = new()
            {
                PublicKey = Array.Empty<byte>()
            };
            LocalPlacementBuilder builder = new(b, localInfo);
            Address address1 = RandomAddress();
            NodeInfo ni1 = new();
            ni1.Addresses.Add(localAddrs);
            b.NSS.Add(address1, new() { new() { new Node(0, ni1) } });
            var r = builder.BuildPlacement(address1, null);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(1, r[0].Count);
        }

        [TestMethod]
        public void TestIntersect()
        {
            string localAddrs = "/ip4/0.0.0.0/tcp/8080";
            List<Network.Address> localAddresses = new() { Network.Address.FromString("/ip4/0.0.0.0/tcp/8080") };
            NodeInfo ni = new();
            ni.Addresses.Add(localAddrs);
            Node n = new(0, ni);
            var l = n.Addresses.Select(p => Network.Address.FromString(p)).ToList();
            var c = l.Intersect(localAddresses).ToList();
            Assert.AreEqual(1, c.Count);
        }
    }
}
