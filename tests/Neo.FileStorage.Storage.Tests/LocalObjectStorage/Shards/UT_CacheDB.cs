using System.Linq;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.LocalObjectStorage.Shards;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.LocalObjectStorage.Shards
{
    [TestClass]
    public class UT_CacheDB
    {
        private int Unflushed(CacheDB db)
        {
            var count = 0;
            db.IterateUnflushed(d =>
            {
                count++;
                return false;
            });
            return count;
        }
        [TestMethod]
        public void Test()
        {
            string path = "UT_CacheDB.TestPut";
            if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, true);
            var cdb = new CacheDB(path);
            var obj = RandomObject();
            var oi = new ObjectInfo
            {
                Object = obj,
            };
            cdb.Put(oi);
            Assert.IsTrue(obj.ToByteArray().SequenceEqual(cdb.Get(obj.Address)));
        }

        [TestMethod]
        public void TestUnflushed()
        {
            string path = "UT_CacheDB.TestUnflushed";
            if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, true);
            var cdb = new CacheDB(path);
            var addrs = new Address[10];
            for (int i = 0; i < 10; i++)
            {
                var obj = RandomObject();
                var oi = new ObjectInfo
                {
                    Object = obj,
                };
                cdb.Put(oi);
                Assert.IsTrue(obj.ToByteArray().SequenceEqual(cdb.Get(obj.Address)));
                addrs[i] = obj.Address;
            }
            var count = Unflushed(cdb);
            Assert.AreEqual(addrs.Length, count);
            cdb.Flushed(addrs[5]);
            count = Unflushed(cdb);
            Assert.AreEqual(4, count);
            cdb.Dispose();
            cdb = new CacheDB(path);
            count = Unflushed(cdb);
            Assert.AreEqual(4, count);
            cdb.Flushed(addrs[9]);
            count = Unflushed(cdb);
            Assert.AreEqual(0, count);
            cdb.Dispose();
        }
    }
}
