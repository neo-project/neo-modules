using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_DistributeTarget : TestKit
    {
        private class TestTraverser : ITraverser
        {
            public List<List<Node>> NSSS = new();

            private int index = 0;
            private int success = 0;

            public List<Node> Next()
            {
                if (NSSS.Count <= index) return new();
                return NSSS[index++];
            }

            public void SubmitSuccess()
            {
                Interlocked.Add(ref success, 1);
            }

            public bool Success()
            {
                return NSSS.Count <= success;
            }
        }

        private Func<Node, CancellationToken, IObjectTarget> NewTargetInitilizer(List<string> addrs, IObjectTarget target)
        {
            return (node, token) =>
            {
                if (node.Addresses.Count != addrs.Count) return null;
                for (int i = 0; i < addrs.Count; i++)
                    if (!node.Addresses[i].Equals(addrs[i])) return null;
                return target;
            };
        }

        [TestMethod]
        public void Test()
        {
            var localAddresses = new List<string> { "/ip4/0.0.0.0/tcp/8080" };
            var key = RandomPrivatekey().LoadPrivateKey();
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_DistributeTarget), 3));
            var t = new DistributeTarget
            {
                LocalInfo = new TestLocalInfo { PublicKey = key.PublicKey() },
                TraverserInitializer = () => traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(localAddresses, next),
                Relay = _ => false,
                LocalPool = work_pool,
                RemotePool = work_pool,
            };
            var ni = new NodeInfo();
            ni.Addresses.AddRange(localAddresses);
            ni.PublicKey = ByteString.CopyFrom(key.PublicKey());
            var node = new Node(0, ni);
            traverser.NSSS.Add(new() { node });
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
            var localAddresses = new List<string> { "/ip4/0.0.0.0/tcp/8080" };
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var t = new DistributeTarget
            {
                TraverserInitializer = () => traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(localAddresses, next),
                Relay = _ => true,
            };
            var ni = new NodeInfo();
            ni.Addresses.AddRange(localAddresses);
            traverser.NSSS.Add(new() { new Node(0, ni) });
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
            var localAddresses = new string[] { "/ip4/0.0.0.0/tcp/8080" };
            var key = RandomPrivatekey().LoadPrivateKey();
            var next = new SimpleObjectTarget();
            var traverser = new TestTraverser();
            var validator = new TestObjectValidator();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_DistributeTarget), 5));
            var t = new DistributeTarget
            {
                LocalInfo = new TestLocalInfo { PublicKey = key.PublicKey() },
                TraverserInitializer = () => traverser,
                ObjectValidator = validator,
                NodeTargetInitializer = NewTargetInitilizer(new(), next),
                Relay = _ => throw new Exception(),
                LocalPool = work_pool,
                RemotePool = work_pool,
            };
            var ni = new NodeInfo();
            ni.Addresses.AddRange(localAddresses);
            ni.PublicKey = ByteString.CopyFrom(key.PublicKey());
            traverser.NSSS.Add(new() { new Node(0, ni) });
            var obj = RandomObject(1024);
            t.WriteHeader(obj.CutPayload());
            t.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            t.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            Assert.ThrowsException<InvalidOperationException>(() => t.Close());
        }
    }
}
