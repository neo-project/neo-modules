using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;

namespace Neo.Plugins.Storage.Tests
{
    [TestClass]
    public class StoreTest
    {
        [TestMethod]
        public void TestLevelDb()
        {
            using (var plugin = new Neo.Plugins.Storage.LevelDBStore())
            {
                // Test all with the same store

                TestStorage(plugin.GetStore());

                // Test with different storages

                TestPersistenceWrite(plugin.GetStore());
                TestPersistenceRead(plugin.GetStore(), true);
                TestPersistenceDelete(plugin.GetStore());
                TestPersistenceRead(plugin.GetStore(), false);
            }
        }

        [TestMethod]
        public void TestRocksDb()
        {
            using (var plugin = new Neo.Plugins.Storage.RocksDBStore())
            {
                // Test all with the same store

                TestStorage(plugin.GetStore());

                // Test with different storages

                TestPersistenceWrite(plugin.GetStore());
                TestPersistenceRead(plugin.GetStore(), true);
                TestPersistenceDelete(plugin.GetStore());
                TestPersistenceRead(plugin.GetStore(), false);
            }
        }

        /// <summary>
        /// Test Put/Delete/TryGet/Seek
        /// </summary>
        /// <param name="store">Store</param>
        private void TestStorage(IStore store)
        {
            using (store)
            {
                var ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);

                store.Put(0, new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                CollectionAssert.AreEqual(new byte[] { 0x03, 0x04 }, ret);
                Assert.IsTrue(store.Contains(0, new byte[] { 0x01, 0x02 }));

                ret = store.TryGet(1, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);
                Assert.IsFalse(store.Contains(1, new byte[] { 0x01, 0x02 }));

                store.Delete(0, new byte[] { 0x01, 0x02 });

                ret = store.TryGet(0, new byte[] { 0x01, 0x02 });
                Assert.IsNull(ret);

                // Test seek

                store.Put(1, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                store.Put(1, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                store.Put(1, new byte[] { 0x00, 0x00, 0x02 }, new byte[] { 0x02 });
                store.Put(1, new byte[] { 0x00, 0x00, 0x03 }, new byte[] { 0x03 });
                store.Put(1, new byte[] { 0x00, 0x00, 0x04 }, new byte[] { 0x04 });

                // Seek Forward

                var enumerator = store.Seek(1, new byte[] { 0x00, 0x00, 0x02 }, IO.Caching.SeekDirection.Forward).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x03 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x03 }, enumerator.Current.Value);

                // Seek Backward

                enumerator = store.Seek(1, new byte[] { 0x00, 0x00, 0x02 }, IO.Caching.SeekDirection.Backward).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x02 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x02 }, enumerator.Current.Value);
                Assert.IsTrue(enumerator.MoveNext());
                CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01 }, enumerator.Current.Key);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, enumerator.Current.Value);

                // Seek Backward

                store.Put(2, new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00 });
                store.Put(2, new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x01 });
                store.Put(2, new byte[] { 0x00, 0x01, 0x02 }, new byte[] { 0x02 });

                enumerator = store.Seek(2, new byte[] { 0x00, 0x00, 0x03 }, IO.Caching.SeekDirection.Backward).GetEnumerator();
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
                store.Put(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0x04, 0x05, 0x06 });
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
                store.Delete(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 });
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
                var ret = store.TryGet(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 });

                if (shouldExist) CollectionAssert.AreEqual(new byte[] { 0x04, 0x05, 0x06 }, ret);
                else Assert.IsNull(ret);
            }
        }
    }
}
