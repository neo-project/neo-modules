using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Services.Object.Put;
using FSObject = Neo.FileStorage.API.Object.Object;
using static Neo.FileStorage.Storage.Tests.Helper;
using Neo.FileStorage.Storage.Services.Object.Put.Target;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_LocalTarget
    {
        private class TestLocalStore : ILocalObjectStore
        {
            public FSObject Object;

            public void Put(FSObject obj)
            {
                Object = obj;
            }
        }

        [TestMethod]
        public void Test()
        {
            var store = new TestLocalStore();
            var obj = RandomObject(1024);
            var t = new LocalTarget
            {
                LocalObjectStore = store,
            };
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            t.Close();
            Assert.IsTrue(obj.Equals(store.Object));
        }
    }
}
