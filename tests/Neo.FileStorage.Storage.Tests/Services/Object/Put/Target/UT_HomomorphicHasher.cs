using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.Storage.Services.Object.Put.Target;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_HomomorphicHasher
    {
        [TestMethod]
        public void TestHash()
        {
            var data = new byte[1024];
            var h = new TzHash().ComputeHash(data);
            using var hasher = new HomomorphicHasher();
            hasher.WriteChunk(data);
            Assert.IsTrue(hasher.Hash.SequenceEqual(h));
        }

        [TestMethod]
        public void TestMultiBlocks()
        {
            var data = new byte[1024];
            var h = new TzHash().ComputeHash(data);
            using var hasher = new HomomorphicHasher();
            hasher.WriteChunk(data[..512]);
            hasher.WriteChunk(data[512..]);
            Assert.IsTrue(hasher.Hash.SequenceEqual(h));
        }

        [TestMethod]
        public void TestIntermediate()
        {
            var data = new byte[1024];
            var h = new TzHash().ComputeHash(data);
            using var hasher = new HomomorphicHasher();
            hasher.WriteChunk(data[..512]);
            var _ = hasher.Hash;
            hasher.WriteChunk(data[512..]);
            Assert.IsTrue(hasher.Hash.SequenceEqual(h));
        }

        [TestMethod]
        public void TestLargeData()
        {
            var data = new byte[30000000];
            var random = new Random();
            random.NextBytes(data);
            var t0 = DateTime.UtcNow;
            var h0 = new TzHash().ComputeHash(data);
            var t1 = DateTime.UtcNow;
            Console.WriteLine(t1 - t0);
            using var hasher = new HomomorphicHasher();
            var t2 = DateTime.UtcNow;
            for (int i = 0; i < data.Length / 1000000; i++)
            {
                hasher.WriteChunk(data[(i * 1000000)..((i + 1) * 1000000)]);
            }
            var hash = hasher.Hash;
            Console.WriteLine(DateTime.UtcNow - t2);
            Assert.AreEqual(h0.ToHexString(), hash.ToHexString());
        }
    }
}
