using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;

namespace neo_plugins.Tests
{
    [TestClass]
    public class StoreTest
    {
        [TestMethod]
        public void TestLevelDb()
        {
            TestStorage(new Neo.Plugins.Storage.LevelDBStore());
        }

        [TestMethod]
        public void TestRocksDb()
        {
            TestStorage(new Neo.Plugins.Storage.RocksDBStore());
        }

        private void TestStorage(IStoragePlugin plugin)
        {
            using (var store = plugin.GetStore())
            {
                var ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);

                store.Put(0, new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                CollectionAssert.AreEqual(new byte[] { 0x03, 0x04 }, ret);

                ret = store.TryGet(1, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);

                store.Delete(0, new byte[] { 0x01, 0x02 });

                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);
            }
        }
    }
}
