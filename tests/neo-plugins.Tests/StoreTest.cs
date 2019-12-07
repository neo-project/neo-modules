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
            using (var plugin = new Neo.Plugins.Storage.LevelDBStore())
            {
                // Test all with the same store

                TestStorage(plugin);

                // Test with different storages

                TestPersistenceWriteReadDelete(plugin);

                // Test snapshot

                TestPersistenceSnapshot(plugin, true);
                TestPersistenceSnapshot(plugin, false);
            }
        }

        [TestMethod]
        public void TestRocksDb()
        {
            using (var plugin = new Neo.Plugins.Storage.RocksDBStore())
            {
                // Test all with the same store

                TestStorage(plugin);

                // Test with different storages

                TestPersistenceWriteReadDelete(plugin);

                // Test snapshot

                TestPersistenceSnapshot(plugin, true);
                TestPersistenceSnapshot(plugin, false);
            }
        }

        [TestMethod]
        public void TestFasterDb()
        {
            using (var plugin = new Neo.Plugins.Storage.FasterDBStore())
            {
                // Test all with the same store

                TestStorage(plugin);

                // Test with different storages

                TestPersistenceWriteReadDelete(plugin);

                // Test snapshot

                TestPersistenceSnapshot(plugin, true);
                TestPersistenceSnapshot(plugin, false);
            }
        }

        /// <summary>
        /// Test Put/Delete/TryGet
        /// </summary>
        /// <param name="plugin">Plugin</param>
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

        /// <summary>
        /// Test Put
        /// </summary>
        /// <param name="plugin">Plugin</param>
        private void TestPersistenceWriteReadDelete(IStoragePlugin plugin)
        {
            // Put

            using (var store = plugin.GetStore())
            {
                store.Put(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0x04, 0x05, 0x06 });
            }

            // Read

            using (var store = plugin.GetStore())
            {
                var ret = store.TryGet(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 });
                CollectionAssert.AreEqual(new byte[] { 0x04, 0x05, 0x06 }, ret);
            }

            // Delete

            using (var store = plugin.GetStore())
            {
                store.Delete(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 });
            }

            // Read

            using (var store = plugin.GetStore())
            {
                var ret = store.TryGet(byte.MaxValue, new byte[] { 0x01, 0x02, 0x03 });
                Assert.IsNull(ret);
            }
        }

        /// <summary>
        /// Test snapshot
        /// </summary>
        /// <param name="plugin">Plugin</param>
        /// <param name="commit">Commit</param>
        private void TestPersistenceSnapshot(IStoragePlugin plugin, bool commit)
        {
            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0x04, 0x05, 0x06 };

            using (var store = plugin.GetStore())
            {
                store.Put(0x10, key, value);

                using (var snapshot = store.GetSnapshot())
                {
                    snapshot.Delete(0x10, key);
                    if (commit) snapshot.Commit();
                }

                if (commit)
                {
                    Assert.IsNull(store.TryGet(0x10, key));
                }
                else
                {
                    CollectionAssert.AreEqual(value, store.TryGet(0x10, key));
                }
            }

            // Repeat with other store

            using (var store = plugin.GetStore())
            {
                if (commit)
                {
                    Assert.IsNull(store.TryGet(0x10, key));
                }
                else
                {
                    CollectionAssert.AreEqual(value, store.TryGet(0x10, key));
                }
            }

            // Try to write during a snapshot

            using (var store = plugin.GetStore())
            {
                store.Put(0x10, key, value);

                using (var snapshot = store.GetSnapshot())
                {
                    // Create a dummy snapshot and remove from the storage

                    store.Delete(0x10, key);
                }

                Assert.IsNull(store.TryGet(0x10, key));
            }
        }
    }
}
