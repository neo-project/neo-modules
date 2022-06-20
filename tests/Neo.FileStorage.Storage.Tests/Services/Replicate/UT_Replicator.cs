using Akka.TestKit.Xunit2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services;
using Neo.FileStorage.Storage.Services.Replicate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using static Neo.FileStorage.Storage.Tests.Helper;

namespace Neo.FileStorage.Storage.Tests.Services.Replicate
{
    [TestClass]
    public class UT_Replicator : TestKit
    {
        private class TestRemoteSender : IRemoteSender
        {
            public Dictionary<string, FSObject> Objects = new();

            public void PutObject(RemotePutPrm prm, CancellationToken context)
            {
                Objects[string.Join("", prm.Node.Addresses.Select(p => p.ToString()))] = prm.Object;
            }
        }

        private class TestObjectSource : ILocalObjectSource
        {
            public Dictionary<Address, FSObject> Objects = new();

            public FSObject Get(Address address)
            {
                if (Objects.TryGetValue(address, out var obj))
                    return obj;
                throw new ObjectNotFoundException();
            }

            public FSObject Head(Address address, bool raw)
            {
                throw new NotImplementedException();
            }

            public FSObject GetRange(Address address, FSRange range)
            {
                throw new NotImplementedException();
            }
        }

        private class TestCase
        {
            public List<string> Addrs = new();
            public uint Quantity = 0;
            public Address Address;
        }

        [TestMethod]
        public void Test()
        {
            TestRemoteSender remoteSender = new();
            TestObjectSource objectSource = new();
            Replicator.Args args = new()
            {
                RemoteSender = remoteSender,
                LocalStorage = objectSource,
            };
            var replicator = Sys.ActorOf(Replicator.Props(args));
            Random random = new();
            List<TestCase> cases = new();
            for (int i = 0; i < 2; i++)
            {
                Replicator.Task task = new();
                TestCase tc = new();
                var obj = RandomObject(256);
                objectSource.Objects[obj.Address] = obj;
                task.Address = obj.Address;
                tc.Address = task.Address;
                List<Node> ns = new();
                for (int j = 0; j < 3; j++)
                {
                    var addr = $"/ip4/0.0.0.0/tcp/80{i * 3 + j}";
                    tc.Addrs.Add(addr);
                    NodeInfo ni = new();
                    ni.Addresses.Add(addr);
                    ns.Add(new(i, ni));
                }
                task.Nodes = ns;
                task.Quantity = (uint)random.Next() % 5;
                if (task.Quantity == 0) task.Quantity = 1;
                tc.Quantity = task.Quantity;
                replicator.Tell(task, TestActor);
                cases.Add(tc);
            }
            Thread.Sleep(2000);
            foreach (var task in cases)
            {
                var count = Math.Min(task.Quantity, task.Addrs.Count);
                for (int i = 0; i < task.Addrs.Count; i++)
                {
                    if (i < count)
                    {
                        Assert.IsTrue(remoteSender.Objects.TryGetValue(task.Addrs[i], out var obj));
                        Assert.IsTrue(task.Address.Equals(obj.Address));
                    }
                    else
                    {
                        Assert.IsFalse(remoteSender.Objects.ContainsKey(task.Addrs[i]));
                    }
                }
            }
        }

        [TestMethod]
        public void TestNoObject()
        {
            TestRemoteSender remoteSender = new();
            TestObjectSource objectSource = new();
            Replicator.Args args = new()
            {
                RemoteSender = remoteSender,
                LocalStorage = objectSource,
            };
            var replicator = Sys.ActorOf(Replicator.Props(args));
            Random random = new();
            Replicator.Task task = new();
            var obj = RandomObject(256);
            task.Address = obj.Address;
            List<Node> ns = new();
            for (int j = 0; j < 3; j++)
            {
                var addr = $"/ip4/0.0.0.0/tcp/80{j}";
                NodeInfo ni = new();
                ni.Addresses.Add(addr);
                ns.Add(new(j, ni));
            }
            task.Nodes = ns;
            var q = (uint)random.Next() % 5;
            if (q == 0) q = 1;
            task.Quantity = q;
            replicator.Tell(task, TestActor);
            Thread.Sleep(1000);
            Assert.AreEqual(0, remoteSender.Objects.Count);
            objectSource.Objects[obj.Address] = obj;
            replicator.Tell(task, TestActor);
            Thread.Sleep(1000);
            Assert.AreEqual((int)Math.Min(q, 3), remoteSender.Objects.Count);
        }
    }
}
