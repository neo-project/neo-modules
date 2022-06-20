using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Cache;

namespace Neo.FileStorage.Storage.Tests.Cache
{
    [TestClass]
    public class UT_NetmworkCache
    {
        [TestMethod]
        public void TestCache()
        {
            int value = 0;
            bool fetched = false;
            int fetcher(int key)
            {
                fetched = true;
                return key == 0 ? value : key;
            }
            NetworkCache<int, int> cache = new(3, fetcher);
            var val = cache.Get(0);
            Assert.AreEqual(value, val);
            Assert.IsTrue(fetched);
            fetched = false;
            val = cache.Get(0);
            Assert.AreEqual(value, val);
            Assert.IsFalse(fetched);
            for (int i = 1; i <= 3; i++)
            {
                cache.Get(i);
                Assert.IsTrue(fetched);
                fetched = false;
            }
            val = cache.Get(0);
            Assert.AreEqual(value, val);
            Assert.IsTrue(fetched);
        }
    }
}
