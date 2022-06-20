using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Placement;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Tests.Placement
{
    public class TestBuilder : IPlacementBuilder
    {
        private List<List<Node>> vectors;

        public TestBuilder(List<List<Node>> vs)
        {
            vectors = vs;
        }

        public List<List<Node>> BuildPlacement(Address address, PlacementPolicy pp)
        {
            return vectors;
        }
    }

    [TestClass]
    public class UT_Traverser
    {
        private (List<List<Node>>, FSContainer) PreparePlacement(int[] ss, int[] rs)
        {
            var nodes = new List<List<Node>>();
            var replicas = new List<Replica>();
            uint num = 0;
            for (int i = 0; i < ss.Length; i++)
            {
                List<NodeInfo> ns = new();
                for (int j = 0; j < ss[i]; j++)
                {
                    NodeInfo ni = new();
                    ni.Addresses.Add("/ip4/0.0.0.0/tcp/" + num.ToString());
                    ns.Add(ni);
                    num++;
                }
                nodes.Add(ns.Select((p, index) => new Node(index, p)).ToList());
                replicas.Add(new Replica((uint)rs[i], ""));
            }
            var policy = new PlacementPolicy(0, replicas.ToArray(), null, null);
            return (nodes, new FSContainer() { PlacementPolicy = policy });
        }

        private List<List<Node>> CopyVectors(List<List<Node>> v)
        {
            var vc = new List<List<Node>>();
            foreach (var li in v)
                vc.Add(li.ToList());
            return vc;
        }

        [TestMethod]
        public void TestTraverserSearch()
        {
            var selectors = new int[] { 2, 3 };
            var replicas = new int[] { 1, 2 };

            List<List<Node>> nodes;
            FSContainer ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser(
                new TestBuilder(nodesCopy),
                ctn.PlacementPolicy,
                new Address
                {
                    ContainerId = ctn.CalCulateAndGetId,
                },
                trackCopies: false
            );

            for (int i = 0; i < selectors.Length; i++)
            {
                var ns = tr.Next();
                Assert.AreEqual(nodes[i].Count, ns.Count);

                for (int j = 0; j < nodes[i].Count; j++)
                {
                    Assert.AreEqual(ns[j].Addresses[0], nodes[i][j].Addresses[0]);
                }
            }

            Assert.IsTrue(tr.Success());
        }

        [TestMethod]
        public void TestTraverserRead()
        {
            var selectors = new int[] { 5, 3 };
            var replicas = new int[] { 2, 2 };

            List<List<Node>> nodes;
            FSContainer ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser(
                new TestBuilder(nodesCopy),
                ctn.PlacementPolicy,
                new Address
                {
                    ContainerId = ctn.CalCulateAndGetId,
                },
                1
            );
            for (int i = 0; i < nodes[0].Count; i++)
                tr.Next();
            var ns = tr.Next();
            Assert.AreEqual(1, ns.Count);
            Assert.AreEqual(nodes[1][0].Addresses[0], ns[0].Addresses[0]);
        }

        [TestMethod]
        public void TestTraverserPut()
        {
            var selectors = new int[] { 5, 3 };
            var replicas = new int[] { 2, 2 };

            List<List<Node>> nodes;
            FSContainer ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser(
                new TestBuilder(nodesCopy),
                ctn.PlacementPolicy,
                new Address
                {
                    ContainerId = ctn.CalCulateAndGetId,
                }
            );

            void fn(int cv)
            {
                for (int i = 0; i + replicas[cv] < selectors[cv]; i += replicas[cv])
                {
                    var addrs = tr.Next();
                    Assert.AreEqual(replicas[cv], addrs.Count);
                    for (int j = 0; j < addrs.Count; j++)
                    {
                        Assert.AreEqual(nodes[cv][i + j].Addresses[0], addrs[j].Addresses[0]);
                    }
                }
                Assert.IsFalse(tr.Next().Count > 0);
                Assert.IsFalse(tr.Success());
                for (int i = 0; i < replicas[cv]; i++)
                {
                    tr.SubmitSuccess();
                }
            };

            for (int i = 0; i < selectors.Length; i++)
            {
                fn(i);

                if (i < selectors.Length - 1)
                    Assert.IsFalse(tr.Success());
                else
                    Assert.IsTrue(tr.Success());
            }
        }

        [TestMethod]
        public void TestLocalOperation()
        {
            var selectors = new int[] { 2, 3 };
            var replicas = new int[] { 1, 2 };

            List<List<Node>> nodes;
            FSContainer ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var tr = new Traverser(
                new TestBuilder(new List<List<Node>>() { new List<Node> { nodes[1][1] } }),
                ctn.PlacementPolicy,
                new Address
                {
                    ContainerId = ctn.CalCulateAndGetId,
                },
                1
            );
            Assert.IsTrue(tr.Next().Count > 0);
            Assert.IsFalse(tr.Success());
            tr.SubmitSuccess();
            Assert.IsFalse(tr.Next().Count > 0);
            Assert.IsTrue(tr.Success());
        }
    }
}
