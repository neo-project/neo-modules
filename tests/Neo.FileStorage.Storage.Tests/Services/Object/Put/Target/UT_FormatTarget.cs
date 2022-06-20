using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using FSObject = Neo.FileStorage.API.Object.Object;
using static Neo.FileStorage.Storage.Tests.Helper;
using System;
using Google.Protobuf;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Storage.Tests.Services.Object.Put;
using Neo.FileStorage.Storage.Tests;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    [TestClass]
    public class UT_FormatTarget
    {

        [TestMethod]
        public void TestWithoutParent()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            var cid = RandomContainerID();
            var rand = new Random();
            var payload = new byte[1024];
            rand.NextBytes(payload);
            var next = new SimpleObjectTarget();
            TestEpochSource epochSource = new();
            epochSource.Epoch = 13;
            var obj = new FSObject()
            {
                Header = new()
                {
                    ContainerId = cid,
                    OwnerId = OwnerID.FromPublicKey(key.PublicKey()),
                },
                Payload = ByteString.CopyFrom(payload),
            };
            var t = new FormatTarget
            {
                Key = key,
                Next = next,
                SessionToken = null,
                EpochSource = epochSource,
            };
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            t.Close();
            Assert.IsTrue(API.Refs.Version.SDKVersion().Equals(next.Object.Version));
            Assert.AreEqual((ulong)obj.Payload.Length, next.Object.PayloadSize);
            Assert.AreEqual(13ul, next.Object.CreationEpoch);
            Assert.IsNotNull(next.Object.Signature);
            Assert.IsTrue(next.Object.Signature.VerifyMessagePart(next.Object.CalculateID()));
            Assert.AreEqual(obj.Payload.Length, next.Object.Payload.Length);
        }

        [TestMethod]
        public void TestWithParent()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            var cid = RandomContainerID();
            var rand = new Random();
            var child_payload = new byte[1024];
            rand.NextBytes(child_payload);
            var next = new SimpleObjectTarget();
            TestEpochSource epochSource = new();
            epochSource.Epoch = 13;
            var parent = new FSObject()
            {
                Header = new()
                {
                    ContainerId = cid,
                    OwnerId = OwnerID.FromPublicKey(key.PublicKey()),
                },
                Payload = ByteString.Empty,
            };
            var child = new FSObject()
            {
                Header = new()
                {
                    ContainerId = cid,
                    OwnerId = OwnerID.FromPublicKey(key.PublicKey()),
                },
                Payload = ByteString.CopyFrom(child_payload),
            };
            child.Parent = parent;
            var t = new FormatTarget
            {
                Key = key,
                Next = next,
                SessionToken = null,
                EpochSource = epochSource,
            };
            t.WriteHeader(child.CutPayload());
            t.WriteChunk(child.Payload.ToByteArray()[..(child.Payload.Length / 2)]);
            t.WriteChunk(child.Payload.ToByteArray()[(child.Payload.Length / 2)..]);
            var r_parent = t.Close().ParentHeader;
            Assert.AreEqual((ulong)parent.Payload.Length, r_parent.PayloadSize);
            Assert.AreEqual(13ul, r_parent.CreationEpoch);
            Assert.IsNotNull(r_parent.Signature);
            Assert.IsTrue(r_parent.Signature.VerifyMessagePart(r_parent.CalculateID()));
        }
    }
}
