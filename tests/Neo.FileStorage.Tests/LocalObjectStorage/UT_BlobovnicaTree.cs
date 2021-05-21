using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.IO.Data.LevelDB;
using static Neo.FileStorage.Tests.LocalObjectStorage.Helper;

namespace Neo.FileStorage.Tests.LocalObjectStorage
{
    [TestClass]
    public class UT_BlobovnicaTree
    {
        [TestMethod]
        public void TestBlobovnicas()
        {
            string path = "./test_blz";
            ulong size_limit = 2 << 10;
            BlobovniczaTree tree = new(path)
            {
                BlzShallowWidth = 2,
                BlzShallowDepth = 2,
                SmallSizeLimit = size_limit,
            };
            tree.Initialize();
            var objSize = size_limit / 2;
            var minFitObjNum = 2 * 2 * size_limit / objSize;
            List<Address> addrs = new();
            for (ulong i = 0; i < minFitObjNum; i++)
            {
                var obj = RandomObject(objSize);
                addrs.Add(obj.Address);
                var id = tree.Put(obj);
                var res = tree.Get(obj.Address, id);
                Assert.IsTrue(obj.ToByteArray().SequenceEqual(res.ToByteArray()));
                res = tree.Get(obj.Address);
                Assert.IsTrue(obj.ToByteArray().SequenceEqual(res.ToByteArray()));
            }
            Directory.Delete(path, true);
        }
    }
}
