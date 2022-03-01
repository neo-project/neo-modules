using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Session.Storage;
using static Neo.FileStorage.Storage.Tests.Helper;
using static Neo.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Session
{
    [TestClass]
    public class UT_TokenStore
    {
        [TestMethod]
        public void Test()
        {
            OwnerID owner = RandomOwnerID();
            TestDB tokenDb = new();
            TokenStore ts = new(tokenDb);
            CreateRequest req = new()
            {
                Body = new()
                {
                    OwnerId = owner,
                    Expiration = 10
                }
            };
            var token = ts.Create(req).Id.ToByteArray();
            Assert.IsTrue(tokenDb.Mem.ContainsKey(Convert.ToBase64String(Concat(owner.Value.ToByteArray(), token))));
            TokenStore ts1 = new(new TestDB());
            Assert.IsNull(ts1.Get(owner, token));
            TokenStore ts2 = new(tokenDb);
            Assert.IsNotNull(ts2.Get(owner, token));
            ts2.RemoveExpired(5);
            Assert.IsNotNull(ts2.Get(owner, token));
            ts2.RemoveExpired(11);
            Assert.IsNull(ts1.Get(owner, token));
        }
    }
}
