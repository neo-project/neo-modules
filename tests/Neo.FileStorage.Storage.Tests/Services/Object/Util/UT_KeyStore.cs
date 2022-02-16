using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Session;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Util
{
    [TestClass]
    public class UT_KeyStore
    {
        private class TestTokenStorage : ITokenStorage
        {
            public readonly Dictionary<string, PrivateToken> Keys = new();

            public PrivateToken Get(OwnerID owner, byte[] token)
            {
                if (Keys.TryGetValue(owner.ToAddress() + Convert.ToBase64String(token), out var key))
                {
                    return key;
                }
                return null;
            }
        }

        [TestMethod]
        public void TestGet()
        {
            var local = RandomPrivatekey().LoadPrivateKey();
            var key = RandomPrivatekey().LoadPrivateKey();
            var owner = RandomOwnerID();
            var token = Guid.NewGuid().ToByteArray();
            var storage = new TestTokenStorage();
            var kst = new KeyStore(local, storage, new TestEpochSource { Epoch = 13 });
            storage.Keys.Add(owner.ToAddress() + Convert.ToBase64String(token), new() { SessionKey = key, Expiration = ulong.MaxValue });
            var st = new SessionToken
            {
                Body = new()
                {
                    OwnerId = owner,
                    Id = ByteString.CopyFrom(token),
                }
            };
            Assert.AreEqual(key, kst.GetKey(st));
        }

        [TestMethod]
        public void TestGetNotExist()
        {
            var local = RandomPrivatekey().LoadPrivateKey();
            var owner = RandomOwnerID();
            var token = Guid.NewGuid().ToByteArray();
            var storage = new TestTokenStorage();
            var kst = new KeyStore(local, storage, new TestEpochSource { Epoch = 13 });
            var st = new SessionToken
            {
                Body = new()
                {
                    OwnerId = owner,
                    Id = ByteString.CopyFrom(token),
                }
            };
            Assert.ThrowsException<InvalidOperationException>(() => kst.GetKey(st));
        }

        [TestMethod]
        public void TestGetNull()
        {
            var local = RandomPrivatekey().LoadPrivateKey();
            var storage = new TestTokenStorage();
            var kst = new KeyStore(local, storage, null);
            Assert.AreEqual(local, kst.GetKey(null));
        }
    }
}