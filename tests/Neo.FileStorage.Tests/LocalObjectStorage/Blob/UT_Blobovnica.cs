using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.LocalObjectStorage.Blob;
using static Neo.FileStorage.Tests.LocalObjectStorage.Helper;

namespace Neo.FileStorage.Tests.LocalObjectStorage.Blob
{
    [TestClass]
    public class UT_Blobovnica
    {
        [TestMethod]
        public void TestOpenBlob()
        {
            string path = "test_blz/0000000000000000/0000000000000000/0100000000000000";
            using Blobovnicza blob = new(path);
            blob.Open();
            var obj = RandomObject(100);
            blob.Put(obj);
            Assert.IsFalse(blob.Get(obj.Address) is null);
            Directory.Delete(path, true);
        }
    }
}
