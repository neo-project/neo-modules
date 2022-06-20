using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using static Neo.FileStorage.Storage.LocalObjectStorage.Metabase.Helper;

namespace Neo.FileStorage.Storage.Tests.LocalObjectStorage.Metabase
{
    [TestClass]
    public class UT_Helper
    {
        [TestMethod]
        public void TestObjectTypeParse()
        {
            Assert.AreEqual(ObjectType.StorageGroup, "STORAGE_GROUP".ToObjectType());
            Assert.AreEqual(ObjectType.StorageGroup, "StorageGroup".ToObjectType());
            Assert.AreEqual(ObjectType.Tombstone, "Tombstone".ToObjectType());
            Assert.AreEqual(ObjectType.Regular, "Regular".ToObjectType());
        }
    }

}
