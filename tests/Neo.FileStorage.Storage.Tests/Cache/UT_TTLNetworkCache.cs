using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Cache;

namespace Neo.FileStorage.Storage.Tests.Cache
{
    [TestClass]
    public class TestTTLNetworkCache
    {
        [TestMethod]
        public void TestCache()
        {
            int value = 0;
            bool reallyDo = false;
            int fetcher(int i)
            {
                reallyDo = true;
                return value;
            }
            TTLNetworkCache<int, int> cache = new(3, TimeSpan.FromSeconds(3), fetcher);
            int val = cache.Get(0);
            Assert.IsTrue(reallyDo);
            reallyDo = false;
            Assert.AreEqual(value, val);
            val = cache.Get(0);
            Assert.IsFalse(reallyDo);
            Assert.AreEqual(value, val);
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Assert.IsFalse(reallyDo);
            value = 1;
            val = cache.Get(0);
            Assert.IsTrue(reallyDo);
            Assert.AreEqual(value, val);
        }
    }
}
