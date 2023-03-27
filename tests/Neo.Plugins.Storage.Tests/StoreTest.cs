using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using System.Linq;

namespace Neo.Plugins.Storage.Tests
{
    [TestClass]
    public class StoreTest
    {
        private const string PathLeveldb = "Data_LevelDB_UT";
        private const string PathRocksdb = "Data_RocksDB_UT";

        [TestMethod]
        public void TestLevelDb()
        {
            using var plugin = new LevelDBStore();
            TestPersistenceDelete(plugin.GetStore(PathLeveldb));
            // Test all with the same store

            TestStorage(plugin.GetStore(PathLeveldb));

            // Test with different storages

            TestPersistenceWrite(plugin.GetStore(PathLeveldb));
            TestPersistenceRead(plugin.GetStore(PathLeveldb), true);
            TestPersistenceDelete(plugin.GetStore(PathLeveldb));
            TestPersistenceRead(plugin.GetStore(PathLeveldb), false);
        }

        [TestMethod]
        public void TestRocksDb()
        {
            using var plugin = new RocksDBStore();
            TestPersistenceDelete(plugin.GetStore(PathRocksdb));
            // Test all with the same store

            TestStorage(plugin.GetStore(PathRocksdb));

            // Test with different storages

            TestPersistenceWrite(plugin.GetStore(PathRocksdb));
            TestPersistenceRead(plugin.GetStore(PathRocksdb), true);
            TestPersistenceDelete(plugin.GetStore(PathRocksdb));
            TestPersistenceRead(plugin.GetStore(PathRocksdb), false);
        }

        /// <summary>
        /// Test Put/Delete/TryGet/Seek
        /// </summary>
        /// <param name="store">Store</param>
        private void TestStorage(IStore store)
        {
            using (store)
            {
                var key1 = new byte[] { 0x01, 0x02 };
                var value1 = new byte[] { 0x03, 0x04 };

                store.Delete(key1);
                var ret = store.TryGet(key1);
                Assert.IsNull(ret);

                store.Put(key1, value1);
                ret = store.TryGet(key1);
                CollectionAssert.AreEqual(value1, ret);
                Assert.IsTrue(store.Contains(key1));

                ret = store.TryGet(value1);
                Assert.IsNull(ret);
                Assert.IsTrue(store.Contains(key1));

                store.Delete(key1);

                ret = store.TryGet(key1);
                Assert.IsNull(ret);
                Assert.IsFalse(store.Contains(key1));

                // Test seek

                store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                store.Put(new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                store.Put(new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                store.Put(new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });

                // Seek Forward

                var entries = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).ToArray();
                Assert.AreEqual(3, entries.Length);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, entries[0].Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, entries[0].Value);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x03 }, entries[1].Key);
                CollectionAssert.AreEqual(new byte[] { 0x03 }, entries[1].Value);

                // Seek Backward

                entries = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).ToArray();
                Assert.AreEqual(3, entries.Length);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, entries[0].Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, entries[0].Value);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01 }, entries[1].Key);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, entries[1].Value);

                // Seek Backward
                store.Delete(new byte[] { 0x00, 0x00, 0x00 });
                store.Delete(new byte[] { 0x00, 0x00, 0x01 });
                store.Delete(new byte[] { 0x00, 0x00, 0x02 });
                store.Delete(new byte[] { 0x00, 0x00, 0x03 });
                store.Delete(new byte[] { 0x00, 0x00, 0x04 });
                store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                store.Put(new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });

                entries = store.Seek(new byte[] { 0x00, 0x00, 0x03 }, SeekDirection.Backward).ToArray();
                Assert.AreEqual(2, entries.Length);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01 }, entries[0].Key);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, entries[0].Value);
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00 }, entries[1].Key);
                CollectionAssert.AreEqual(new byte[] { 0x00 }, entries[1].Value);
            }
        }

        /// <summary>
        /// Test Put
        /// </summary>
        /// <param name="store">Store</param>
        private void TestPersistenceWrite(IStore store)
        {
            using (store)
            {
                store.Put(new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0x04, 0x05, 0x06 });
            }
        }

        /// <summary>
        /// Test Put
        /// </summary>
        /// <param name="store">Store</param>
        private void TestPersistenceDelete(IStore store)
        {
            using (store)
            {
                store.Delete(new byte[] { 0x01, 0x02, 0x03 });
            }
        }

        /// <summary>
        /// Test Read
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="shouldExist">Should exist</param>
        private void TestPersistenceRead(IStore store, bool shouldExist)
        {
            using (store)
            {
                var ret = store.TryGet(new byte[] { 0x01, 0x02, 0x03 });

                if (shouldExist) CollectionAssert.AreEqual(new byte[] { 0x04, 0x05, 0x06 }, ret);
                else Assert.IsNull(ret);
            }
        }
    }
}
