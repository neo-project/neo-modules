using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using System.IO;

namespace Neo.Plugins.Storage.Tests
{
    [TestClass]
    public class StoreTest
    {
        private const string path_leveldb = "Data_LevelDB_{0}";
        private const string path_rocksdb = "Data_RocksDB_{0}";

        [TestMethod]
        public void TestLevelDb()
        {
            using var plugin = new LevelDBStore();
            TestPersistenceDelete(plugin.GetStore(path_leveldb));
            // Test all with the same store

            TestStorage(plugin.GetStore(path_leveldb));

            // Test with different storages

            TestPersistenceWrite(plugin.GetStore(path_leveldb));
            TestPersistenceRead(plugin.GetStore(path_leveldb), true);
            TestPersistenceDelete(plugin.GetStore(path_leveldb));
            TestPersistenceRead(plugin.GetStore(path_leveldb), false);
        }

        [TestMethod]
        public void TestRocksDb()
        {
            using var plugin = new RocksDBStore();
            TestPersistenceDelete(plugin.GetStore(path_rocksdb));
            // Test all with the same store

            TestStorage(plugin.GetStore(path_rocksdb));

            // Test with different storages

            TestPersistenceWrite(plugin.GetStore(path_rocksdb));
            TestPersistenceRead(plugin.GetStore(path_rocksdb), true);
            TestPersistenceDelete(plugin.GetStore(path_rocksdb));
            TestPersistenceRead(plugin.GetStore(path_rocksdb), false);
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

                var enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Forward).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x03 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x03 }, enumerator.Current.Value);

                // Seek Backward

                enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x02 }, SeekDirection.Backward).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, enumerator.Current.Value);

                // Seek Backward
                store.Delete(new byte[] { 0x00, 0x00, 0x00 });
                store.Delete(new byte[] { 0x00, 0x00, 0x01 });
                store.Delete(new byte[] { 0x00, 0x00, 0x02 });
                store.Delete(new byte[] { 0x00, 0x00, 0x03 });
                store.Delete(new byte[] { 0x00, 0x00, 0x04 });
                store.Put(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                store.Put(new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                store.Put(new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });

                enumerator = store.Seek(new byte[] { 0x00, 0x00, 0x03 }, SeekDirection.Backward).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x00 }, enumerator.Current.Value);
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
