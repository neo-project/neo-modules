using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Cache;

namespace Neo.FileStorage.Storage.Tests.Cache
{
    [TestClass]
    public class TestNetmapCache
    {
        private class TestEpochSource : IEpochSource
        {
            public ulong Epoch;

            public ulong CurrentEpoch => Epoch;
        }

        private class TestNetmapSource : INetmapSource
        {
            public readonly Dictionary<ulong, NetMap> NetMaps = new();
            public NetMap GetNetMapByEpoch(ulong epoch)
            {
                if (NetMaps.TryGetValue(epoch, out var nm))
                    return nm;
                throw new Exception();
            }
        }

        [TestMethod]
        public void TestCache()
        {
            TestEpochSource epochSource = new();
            epochSource.Epoch = 10;
            TestNetmapSource netmapSource = new();
            netmapSource.NetMaps[10] = new NetMap(new());
            var nc = new NetmapCache(3, epochSource, netmapSource);
            Assert.IsNotNull(nc.GetNetMapByEpoch(10));
            Assert.IsNotNull(nc.GetNetMapByDiff(0));
            Assert.ThrowsException<Exception>(() => nc.GetNetMapByDiff(1));
        }
    }
}
