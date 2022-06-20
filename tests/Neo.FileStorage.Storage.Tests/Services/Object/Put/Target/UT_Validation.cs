using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using System;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_Validation
    {
        [TestMethod]
        public void TestValidation()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(1024);
            v.WriteHeader(obj.CutPayload());
            v.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            v.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            v.Close();
        }

        [TestMethod]
        public void TestValidationWrongSize()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(1024);
            obj.Header.PayloadLength = 1000;
            Assert.ThrowsException<InvalidOperationException>(() => v.WriteHeader(obj));
        }

        [TestMethod]
        public void TestValidationSizeLimit()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1000
            };
            var obj = RandomObject(1024);
            Assert.ThrowsException<SizeExceedLimitException>(() => v.WriteHeader(obj));
        }

        [TestMethod]
        public void TestValidationChecksumType()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(1024);
            obj.Header.PayloadHash.Type = API.Refs.ChecksumType.Unspecified;
            Assert.ThrowsException<InvalidOperationException>(() => v.WriteHeader(obj));
        }

        [TestMethod]
        public void TestValidationValidateFail()
        {
            var validator = new TestObjectValidator();
            validator.ValidateResult = VerifyResult.Null;
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(1024);
            Assert.ThrowsException<FormatException>(() => v.WriteHeader(obj));
        }

        [TestMethod]
        public void TestValidationWrongChunk()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 2048
            };
            var obj = RandomObject(1024);
            v.WriteHeader(obj.CutPayload());
            v.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            v.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            Assert.ThrowsException<InvalidOperationException>(() => v.WriteChunk(new byte[] { 0x01 }));
            v.Close();
        }

        [TestMethod]
        public void TestValidationWrongHash()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(1024);
            obj.Header.PayloadHash.Sum = ByteString.CopyFrom(new byte[] { 0x01 }.Sha256());
            v.WriteHeader(obj.CutPayload());
            v.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            v.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            Assert.ThrowsException<InvalidOperationException>(() => v.Close());
        }

        [TestMethod]
        public void TestEmptyPayload()
        {
            var validator = new TestObjectValidator();
            var next = new SimpleObjectTarget();
            var v = new ValidationTarget
            {
                Next = next,
                ObjectValidator = validator,
                MaxObjectSize = 1025
            };
            var obj = RandomObject(0);
            v.WriteHeader(obj);
            var r = v.Close();
            Assert.AreEqual(r.Self, obj.ObjectId);
        }
    }
}
