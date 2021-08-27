using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using System;
using System.Collections.Generic;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_DistributeTarget
    {
        private class TestTraverser : ITraverser
        {
            public List<List<List<Network.Address>>> NSSS = new();

            private int index = 0;
            private int success = 0;

            public List<List<Network.Address>> Next()
            {
                if (NSSS.Count <= index) return new();
                return NSSS[index++];
            }

            public void SubmitSuccess()
            {
                ++success;
            }

            public bool Success()
            {
                return NSSS.Count <= success;
            }
        }

        private Func<List<Network.Address>, IObjectTarget> NewTargetInitilizer(List<Network.Address> addresses, IObjectTarget target)
        {
            return addrs =>
            {
                if (addrs.Count != addresses.Count) return null;
                for (int i = 0; i < addresses.Count; i++)
                    if (!addrs[i].Equals(addresses[i])) return null;
                return target;
            };
        }

        private void Relay(List<Network.Address> _) { }

        [TestMethod]
        public void Test()
        {
            var localAddresses = new List<Network.Address> { Network.Address.FromString("/ip4/0.0.0.0/tcp/8080") };
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var t = new DistributeTarget
            {
                Traverser = traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(localAddresses, next),
                Relay = Relay,
            };
            traverser.NSSS.Add(new() { localAddresses });
            var obj = RandomObject(1024);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            t.Close();
            Assert.IsTrue(obj.Equals(next.Object));
        }

        [TestMethod]
        public void TestInvalidObject()
        {
            var localAddresses = new List<Network.Address> { Network.Address.FromString("/ip4/0.0.0.0/tcp/8080") };
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var t = new DistributeTarget
            {
                Traverser = traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(localAddresses, next),
                Relay = Relay,
            };
            traverser.NSSS.Add(new() { localAddresses });
            validator.ContentResult = false;
            var obj = RandomObject(1024);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            Assert.ThrowsException<InvalidOperationException>(() => t.Close());
        }

        [TestMethod]
        public void TestIncomplete()
        {
            var localAddresses = new List<Network.Address> { Network.Address.FromString("/ip4/0.0.0.0/tcp/8080") };
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var t = new DistributeTarget
            {
                Traverser = traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(new(), next),
                Relay = Relay,
            };
            traverser.NSSS.Add(new() { localAddresses });
            var obj = RandomObject(1024);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            Assert.ThrowsException<InvalidOperationException>(() => t.Close());
        }
    }
}
