using System;
using System.Linq;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.LocalObjectStorage.Blob
{
    [TestClass]
    public class UT_Compressor
    {
        [TestMethod]
        public void TestZstdMagic()
        {
            var data = new byte[1024];
            var random = new Random();
            random.NextBytes(data);
            var zstd = new ZstdCompressor();
            var compressed = zstd.Compress(data);
            Assert.IsTrue(zstd.IsCompressed(compressed));
        }

        [TestMethod]
        public void TestCompressWrapperCompressed()
        {
            var obj = RandomObject();
            var raw = obj.ToByteArray();
            var wrapper = new CompressorWrapper(new ZstdCompressor());
            var result = wrapper.Decompress(raw);
            Assert.AreEqual(raw, result);
        }

        [TestMethod]
        public void TestCompressWrapperuncompressed()
        {
            var obj = RandomObject();
            var raw = obj.ToByteArray();
            var zstd = new ZstdCompressor();
            var data = zstd.Compress(raw);
            var wrapper = new CompressorWrapper(zstd);
            var result = wrapper.Decompress(data);
            Assert.IsTrue(result.SequenceEqual(raw));
        }
    }
}
