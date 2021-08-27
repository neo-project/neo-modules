using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using System.Collections.Generic;
using System.Linq;
using FSObject = Neo.FileStorage.API.Object.Object;
using static Neo.FileStorage.Storage.Tests.Helper;
using Neo.FileStorage.API.Cryptography;
using System;
using Neo.Cryptography;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_PayloadSizeLimiterTarget
    {
        private class MultiObjectTarget : IObjectTarget
        {
            public List<FSObject> Objects = new();
            private FSObject current;
            private ByteString payload = ByteString.Empty;

            public void WriteHeader(FSObject obj)
            {
                current = obj;
            }

            public void WriteChunk(byte[] chunk)
            {
                payload = ByteString.CopyFrom(payload.Concat(chunk).ToArray());
            }

            public AccessIdentifiers Close()
            {
                current.Payload = ByteString.CopyFrom(payload.ToByteArray());
                current.ObjectId = current.CalculateID();
                Objects.Add(current);
                payload = ByteString.Empty;
                return new()
                {
                    Parent = current.ParentId,
                    Self = current.ObjectId,
                    ParentHeader = current.Parent,
                };
            }

            public void Dispose() { }
        }

        [TestMethod]
        public void TestSmallObject()
        {
            const ulong MaxObjectSize = 512;
            var mt = new MultiObjectTarget();
            var t = new PayloadSizeLimiterTarget(MaxObjectSize, mt);
            var obj = RandomObject((int)MaxObjectSize / 2);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            var r = t.Close();
            Assert.IsTrue(obj.ContainerId.Equals(mt.Objects[0].ContainerId));
            Assert.IsTrue(obj.OwnerId.Equals(mt.Objects[0].OwnerId));
            Assert.IsTrue(obj.ObjectType.Equals(mt.Objects[0].ObjectType));
            Assert.IsNotNull(mt.Objects[0].PayloadChecksum);
            Assert.IsTrue(obj.Payload.Sha256Checksum().Equals(mt.Objects[0].PayloadChecksum));
        }

        [TestMethod]
        public void TestLargeObject()
        {
            const ulong MaxObjectSize = 512;
            var mt = new MultiObjectTarget();
            var t = new PayloadSizeLimiterTarget(MaxObjectSize, mt);
            var obj = RandomObject((int)MaxObjectSize * 2);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            var r = t.Close();
            Assert.AreEqual(3, mt.Objects.Count);
            Assert.IsTrue(obj.ContainerId.Equals(mt.Objects[0].ContainerId));
            Assert.IsTrue(obj.OwnerId.Equals(mt.Objects[0].OwnerId));
            Assert.IsTrue(obj.ObjectType.Equals(mt.Objects[0].ObjectType));
            Assert.IsNotNull(mt.Objects[0].PayloadChecksum);
            Assert.AreEqual(obj.Payload.Sha256().ToByteArray().ToHexString(), r.ParentHeader.PayloadChecksum.Sum.ToByteArray().ToHexString());
        }
    }
}
