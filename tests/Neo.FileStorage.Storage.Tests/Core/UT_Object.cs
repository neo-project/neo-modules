using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Tests.Core
{
    [TestClass]
    public class UT_Object
    {
        [TestMethod]
        public void TestObjectNull()
        {
            FSObject obj = null;
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.Null, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectNoID()
        {
            FSObject obj = new();
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.InvalidID, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectNoHeader()
        {
            FSObject obj = new()
            {
                ObjectId = RandomObjectID(),
            };
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.NoHeader, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectNoContainerID()
        {
            FSObject obj = new()
            {
                Header = new(),
                ObjectId = RandomObjectID(),
            };
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.InvalidContainerID, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectAttributeDuplicate()
        {
            FSObject obj = new()
            {
                Header = new()
                {
                    ContainerId = RandomContainerID(),
                },
                ObjectId = RandomObjectID(),
            };
            var attr = new API.Object.Header.Types.Attribute
            {
                Key = "City",
                Value = "Shanghai",
            };
            obj.Header.Attributes.Add(attr);
            obj.Header.Attributes.Add(attr);
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.DuplicateAttribute, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectAttributeEmpty()
        {
            FSObject obj = new()
            {
                Header = new()
                {
                    ContainerId = RandomContainerID(),
                },
                ObjectId = RandomObjectID(),
            };
            var attr = new API.Object.Header.Types.Attribute
            {
                Key = "City",
                Value = "",
            };
            obj.Header.Attributes.Add(attr);
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.EmptyAttributeValue, v.Validate(obj));
        }

        [TestMethod]
        public void TestObjectInvalidKey()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            var owner = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash());
            FSObject obj = new()
            {
                Header = new()
                {
                    ContainerId = RandomContainerID(),
                    OwnerId = owner,
                },
                Payload = ByteString.CopyFrom(new byte[] { 0x01 }),
            };
            obj.SetVerificationFields(key);
            ObjectValidator v = new();
            Assert.AreEqual(VerifyResult.Success, v.Validate(obj));
            obj.Header.OwnerId = RandomOwnerID();
            Assert.AreEqual(VerifyResult.InvalidKey, v.Validate(obj));
            obj.Header.SessionToken = new()
            {
                Body = new()
                {
                    SessionKey = ByteString.CopyFrom(RandomPrivatekey().LoadPrivateKey().PublicKey()),
                }
            };
            Assert.AreEqual(VerifyResult.InvalidKey, v.Validate(obj));
        }
    }
}
