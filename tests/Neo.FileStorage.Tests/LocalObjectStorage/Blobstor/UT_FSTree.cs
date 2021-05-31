using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage;
using Neo.FileStorage.LocalObjectStorage.Blobstor;

namespace Neo.FileStorage.Tests.LocalObjectStorage.Blobstor
{
    [TestClass]
    public class UT_FSTree
    {
        [TestMethod]
        public void TestFSTree()
        {
            string root_dir = "./fstree_test";
            FSTree fs = new()
            {
                RootPath = root_dir,
                Depth = 2,
                DirNameLen = 2,
            };
            List<Address> addrs = new();
            int count = 3;
            for (int i = 0; i < count; i++)
            {
                Address address = Helper.RandomAddress();
                byte[] data = new byte[10];
                fs.Put(address, data);
            }
            foreach (var address in addrs)
            {
                Assert.IsNotNull(fs.Get(address));
            }
            Assert.ThrowsException<ObjectFileNotFoundException>(() => fs.Get(Helper.RandomAddress()));
            foreach (var address in addrs)
            {
                Assert.IsTrue(0 < fs.Exists(address).Length);
            }
            Assert.ThrowsException<ObjectFileNotFoundException>(() => fs.Exists(Helper.RandomAddress()));
            fs.Iterate((address, data) =>
            {
                Assert.IsTrue(addrs.Contains(address));
            });
            foreach (var address in addrs)
            {
                fs.Delete(address);
            }
            Directory.Delete(root_dir, true);
        }
    }
}
