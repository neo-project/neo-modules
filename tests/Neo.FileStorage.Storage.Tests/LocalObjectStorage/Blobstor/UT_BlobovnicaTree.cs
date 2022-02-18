using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.IO.Data.LevelDB;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.Tests.LocalObjectStorage.Blobstor
{
    [TestClass]
    public class UT_BlobovnicaTree
    {
        [TestMethod]
        public void TestBlobovnicas()
        {
            string path = "./UT_Blob.TestBlob";
            int size_limit = 2 << 10;
            using BlobovniczaTree tree = new(path, new()
            {
                ShallowDepth = 2,
                ShallowWidth = 2,
            }, (ulong)size_limit);
            tree.Open();
            try
            {
                var objSize = size_limit / 2;
                var minFitObjNum = 2 * 2 * size_limit / objSize;
                List<Address> addrs = new();
                for (int i = 0; i < minFitObjNum; i++)
                {
                    var obj = RandomObject(objSize);
                    addrs.Add(obj.Address);
                    var id = tree.Put(obj.Address, obj.ToByteArray());
                    var res = tree.Get(obj.Address, id);
                    Assert.IsTrue(obj.ToByteArray().SequenceEqual(res));
                    res = tree.Get(obj.Address);
                    Assert.IsTrue(obj.ToByteArray().SequenceEqual(res));
                    var range = new FSRange
                    {
                        Offset = obj.PayloadSize / 3,
                        Length = obj.PayloadSize / 3
                    };
                }
                foreach (var address in addrs)
                {
                    tree.Delete(address);
                    Assert.ThrowsException<ObjectNotFoundException>(() => tree.Get(address));
                    Assert.ThrowsException<ObjectNotFoundException>(() => tree.Delete(address));
                }
            }
            finally
            {
                Directory.Delete(path, true);
            }
        }

        private void TestCase(byte depth, byte width)
        {
            string path = "./UT_TestBlob.TestDir";
            using BlobovniczaTree tree = new(path, new()
            {
                ShallowDepth = depth,
                ShallowWidth = width,
            }, 2 << 10);
            tree.Open();
            try
            {
                var paths = CollectDir(path);
                PrintPaths(paths);
                Assert.AreEqual(Math.Pow(width, depth), CountOfLevel(paths, depth, 0));
                Assert.AreEqual(0, CountOfLevel(paths, depth, 1));
                var obj = RandomObject(10);
                tree.Put(obj.Address, obj.ToByteArray());
                paths = CollectDir(path);
                PrintPaths(paths);
                Assert.AreEqual(Math.Pow(width, depth), CountOfLevel(paths, depth, 0));
                Assert.AreEqual(0, CountOfLevel(paths, depth, 1));

            }
            finally
            {
                tree.Dispose();
                Directory.Delete(path, true);
            }
        }

        public List<string> CollectDir(string path)
        {
            List<string> paths = new() { path };
            for (int i = 0; i < paths.Count; i++)
            {
                foreach (var sub in Directory.GetDirectories(paths[i]))
                {
                    paths.Add(sub);
                }
            }
            return paths.Skip(1).Select(p => p[(path.Length + 1)..]).ToList();
        }

        public int CountOfLevel(List<string> paths, int level, int op = 0)
        {
            int count = 0;
            foreach (var p in paths)
            {
                switch (op)
                {
                    case 0:
                        if (p.Split("/").Length == level) count++;
                        break;
                    case 1:
                        if (p.Split("/").Length > level) count++;
                        break;
                    case 2:
                        if (p.Split("/").Length < level) count++;
                        break;
                }

            }
            return count;
        }

        public void PrintPaths(List<string> paths)
        {
            foreach (var p in paths)
                Console.WriteLine(p);
        }

        [TestMethod]
        public void TestDirectory()
        {
            for (byte i = 1; i < 3; i++)
            {
                for (byte j = 1; j < 3; j++)
                    TestCase(i, j);
            }
        }
    }
}
