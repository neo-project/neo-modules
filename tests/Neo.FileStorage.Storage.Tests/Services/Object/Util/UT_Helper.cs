using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using static Neo.FileStorage.Storage.Helper;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Acl
{
    [TestClass]
    public class UT_Helper
    {
        private RequestMetaHeader GenerateMetaHeader(int depth, SessionToken session, BearerToken bearer)
        {
            RequestMetaHeader meta = new();
            meta.SessionToken = session;
            meta.BearerToken = bearer;
            for (int i = 0; i < depth; i++)
            {
                var link = meta;
                meta = new();
                meta.Origin = link;
            }
            return meta;
        }

        [TestMethod]
        public void TestOriginTokens()
        {
            var session = RandomSessionToken();
            var bearer = RandomBearerToken();
            for (int i = 0; i < 100; i++)
            {
                var meta = GenerateMetaHeader(i, session, bearer);
                Assert.AreEqual(session, OriginalSessionToken(meta));
                Assert.AreEqual(bearer, OriginalBearerToken(meta));
            }
        }

        [TestMethod]
        public void TestMergeSplitInfo()
        {
            SplitInfo si = new();
            SplitInfo other = new()
            {
                LastPart = RandomObjectID(),
                Link = RandomObjectID(),
                SplitId = new SplitID(),
            };
            si.MergeFrom(other);
            Assert.AreEqual(other.LastPart, si.LastPart);
            Assert.AreEqual(other.Link, si.Link);
            Assert.AreEqual(other.SplitId, si.SplitId);
            si = null;
            Assert.ThrowsException<NullReferenceException>(() => si.MergeFrom(other));
        }
    }
}
