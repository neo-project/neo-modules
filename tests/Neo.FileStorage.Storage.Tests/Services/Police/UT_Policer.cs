using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Head;
using Neo.FileStorage.Storage.Services.Police;
using Neo.FileStorage.Storage.Services.Replicate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests.Services.Police
{
    [TestClass]
    public class UT_Policer : TestKit
    {
        private class TestObjectListSource : IObjectListSource
        {
            public List<API.Refs.Address> Addresses = new();

            public List<API.Refs.Address> List(ulong limit)
            {
                return Addresses.Take((int)limit).ToList();
            }
        }

        private class TestRemoteHeader : IRemoteHeader
        {
            public Dictionary<string, FSObject> Headers = new();

            public FSObject Head(RemoteHeadPrm prm, CancellationToken context)
            {
                var key = string.Join("", prm.Address.String(), string.Join("", prm.Node.Addresses.Select(p => p.ToString())));
                if (Headers.TryGetValue(key, out var header))
                    return header;
                throw new InvalidOperationException("remote header couldnot get");
            }
        }

        private class TestReplicator : UntypedActor
        {
            private readonly Action<Replicator.Task> checker;

            public TestReplicator(Action<Replicator.Task> c)
            {
                checker = c;
            }

            protected override void OnReceive(object message)
            {
                switch (message)
                {
                    case Replicator.Task task:
                        checker(task);
                        break;
                    default:
                        throw new InvalidOperationException("invalid message type");
                }
            }

            public static Props Props(Action<Replicator.Task> checker)
            {
                return Akka.Actor.Props.Create(() => new TestReplicator(checker));
            }
        }

        [TestMethod]
        public void Test()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestContainerSource containerSource = new();
            TestObjectListSource objectLister = new();
            TestPlacementBuilder builder = new();
            TestRemoteHeader remoteHeader = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(2, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new API.Client.ContainerWithSignature
            {
                Container = container,
            };
            var obj = RandomObject(cid, 1024);
            objectLister.Addresses.Add(obj.Address);
            remoteHeader.Headers.Add(string.Join("", obj.Address.String(), string.Join("", addrss[0][0])), obj.CutPayload());
            builder.Vectors[obj.Address] = nss;
            Policer.Args args = new()
            {
                LocalInfo = localInfo,
                ContainerSoruce = containerSource,
                ReplicatorRef = TestActor,
                LocalStorage = objectLister,
                PlacementBuilder = builder,
                RemoteHeader = remoteHeader,
                RedundantCopyCallback = null,
            };
            var policer = Sys.ActorOf(Policer.Props(args));
            policer.Tell(new Policer.Trigger());
            var task = ExpectMsg<Replicator.Task>();
            Assert.AreEqual(1u, task.Quantity);
            Assert.AreEqual(obj.Address, task.Address);
        }

        [TestMethod]
        public void TestLocal()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            var localAddr = "/ip4/0.0.0.0/tcp/8080";
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestContainerSource containerSource = new();
            TestObjectListSource objectLister = new();
            TestPlacementBuilder builder = new();
            TestRemoteHeader remoteHeader = new();
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(2, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new API.Client.ContainerWithSignature
            {
                Container = container,
            };
            var obj = RandomObject(cid, 1024);
            objectLister.Addresses.Add(obj.Address);
            remoteHeader.Headers.Add(string.Join("", obj.Address.String(), localAddr), obj.CutPayload());
            NodeInfo ni = new();
            ni.Addresses.Add(localAddr);
            builder.Vectors[obj.Address] = new List<List<Node>>() { new List<Node>() { new Node(0, ni) } };
            Policer.Args args = new()
            {
                LocalInfo = localInfo,
                ContainerSoruce = containerSource,
                ReplicatorRef = TestActor,
                LocalStorage = objectLister,
                PlacementBuilder = builder,
                RemoteHeader = remoteHeader,
                RedundantCopyCallback = null,
            };
            var policer = Sys.ActorOf(Policer.Props(args));
            policer.Tell(new Policer.Trigger());
            var task = ExpectMsg<Replicator.Task>();
            Assert.AreEqual(1u, task.Quantity);
            Assert.AreEqual(obj.Address, task.Address);
        }

        [TestMethod]
        public void TestLocalAbundant()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            var localAddr = "/ip4/0.0.0.0/tcp/8080";
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestContainerSource containerSource = new();
            TestObjectListSource objectLister = new();
            TestPlacementBuilder builder = new();
            TestRemoteHeader remoteHeader = new();
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(2, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new API.Client.ContainerWithSignature
            {
                Container = container,
            };
            var obj = RandomObject(cid, 1024);
            objectLister.Addresses.Add(obj.Address);
            remoteHeader.Headers.Add(string.Join("", obj.Address.String(), localAddr), obj.CutPayload());
            NodeInfo ni = new();
            ni.Addresses.Add(localAddr);
            builder.Vectors[obj.Address] = new List<List<Node>>() { new List<Node>() { new Node(0, ni) } };
            var obj1 = RandomObject(cid, 1024);
            objectLister.Addresses.Add(obj1.Address);
            var addr1 = "/ip4/0.0.0.0/tcp/8081";
            NodeInfo ni1 = new();
            ni1.Addresses.Add(addr1);
            remoteHeader.Headers.Add(string.Join("", obj1.Address.String(), addr1), obj1.CutPayload());
            var addr2 = "/ip4/0.0.0.0/tcp/8082";
            NodeInfo ni2 = new();
            ni2.Addresses.Add(addr2);
            remoteHeader.Headers.Add(string.Join("", obj1.Address.String(), addr2), obj1.CutPayload());
            NodeInfo ni3 = new();
            ni3.Addresses.Add(localAddr);
            ni3.PublicKey = ByteString.CopyFrom(key.PublicKey());
            remoteHeader.Headers.Add(string.Join("", obj1.Address.String(), localAddr), obj1.CutPayload());
            builder.Vectors[obj1.Address] = new List<List<Node>>() { new List<Node>() { new Node(0, ni1), new Node(0, ni2), new Node(0, ni3) } };
            bool called = false;
            Policer.Args args = new()
            {
                LocalInfo = localInfo,
                ContainerSoruce = containerSource,
                ReplicatorRef = TestActor,
                LocalStorage = objectLister,
                PlacementBuilder = builder,
                RemoteHeader = remoteHeader,
                RedundantCopyCallback = _ => called = true,
            };
            var policer = Sys.ActorOf(Policer.Props(args));
            policer.Tell(new Policer.Trigger());
            var task = ExpectMsg<Replicator.Task>();
            Assert.AreEqual(1u, task.Quantity);
            Assert.AreEqual(obj.Address, task.Address);
            Assert.IsTrue(called);
        }
    }
}
