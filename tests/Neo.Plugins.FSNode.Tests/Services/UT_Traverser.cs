using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FSNode.Services.ObjectManager.Placement;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.FSNode.Tests
{
    public class TestBuilder : IBuilder
    {
        private List<List<Node>> vectors;

        public TestBuilder(List<List<Node>> vs)
        {
            this.vectors = vs;
        }

        public List<List<Node>> BuildPlacement(Address addr, PlacementPolicy pp)
        {
            return this.vectors;
        }
    }

    [TestClass]
    public class UT_Traverser
    {
        private (List<List<Node>>, Container) PreparePlacement(int[] ss, int[] rs)
        {
            var nodes = new List<List<Node>>();
            var replicas = new Replica[0];
            uint num = 0;

            for (int i = 0; i < ss.Length; i++)
            {
                var ns = new NodeInfo[0];
                for (int j = 0; j < ss[i]; j++)
                {
                    ns = ns.Append(PrepareNode(num)).ToArray();
                    num++;
                }
                nodes.Add(NodesFromInfo(ns));

                var r = new Replica() { Count = (uint)rs[i] };
                replicas = replicas.Append(r).ToArray();
            }

            var policy = new PlacementPolicy(0, replicas, null, null);

            return (nodes, new Container() { PlacementPolicy = policy });
        }

        private NodeInfo PrepareNode(uint v)
        {
            return new NodeInfo() { Address = "/ip4/0.0.0.0/tcp/" + v.ToString() };
        }

        private List<Node> NodesFromInfo(NodeInfo[] infos)
        {
            var nodes = new List<Node>();
            for (int i = 0; i < infos.Length; i++)
            {
                nodes[i] = new Node(i, infos[i]);
            }
            return nodes;
        }

        private List<List<Node>> CopyVectors(List<List<Node>> v)
        {
            var vc = new List<List<Node>>();
            for (int i = 0; i < v.Count; i++)
            {
                var ns = new List<Node>();
                v[i].ForEach(n => ns.Add(n));
                vc.Add(ns);
            }
            return vc;
        }

        [TestMethod]
        public void TestTraverserSearch()
        {
            var selectors = new int[] { 2, 3 };
            var replicas = new int[] { 1, 2 };

            List<List<Node>> nodes;
            Container ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser
            {
                Address = new Address
                {
                    ContainerId = ctn.CalCulateAndGetID,
                },
                Policy = ctn.PlacementPolicy,
                Builder = new TestBuilder(nodesCopy),
                TrackCopies = true,
            };

            for (int i = 0; i < selectors.Length; i++)
            {
                var addrs = tr.Next();
                Assert.AreEqual(nodes[i].Count, addrs.Length);

                for (int j = 0; j < nodes[i].Count; j++)
                {
                    Assert.AreEqual(addrs[j].String(), nodes[i][j].NetworkAddress);
                }
            }

            Assert.IsTrue(tr.Success());
        }

        private delegate void Fn(int curVector);

        [TestMethod]
        public void TestTraverserRead()
        {
            var selectors = new int[] { 5, 3 };
            var replicas = new int[] { 2, 2 };

            List<List<Node>> nodes;
            Container ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser
            {
                Address = new Address
                {
                    ContainerId = ctn.CalCulateAndGetID,
                },
                Builder = new TestBuilder(nodesCopy),
                FlatSuccess = 1,
            };

            Fn fn = (cv) =>
            {
                for (int i = 0; i < selectors[cv]; i++)
                {
                    var addrs = tr.Next();
                    Assert.AreEqual(1, addrs.Length);
                    Assert.AreEqual(nodes[cv][i].NetworkAddress, addrs[0].String());
                }

                Assert.IsFalse(tr.Success());
                tr.SubmitSuccess();
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
        public void TestTraverserPut()
        {
            var selectors = new int[] { 5, 3 };
            var replicas = new int[] { 2, 2 };

            List<List<Node>> nodes;
            Container ctn;
            (nodes, ctn) = PreparePlacement(selectors, replicas);

            var nodesCopy = CopyVectors(nodes);

            var tr = new Traverser
            {
                Address = new Address
                {
                    ContainerId = ctn.CalCulateAndGetID,
                },
                Builder = new TestBuilder(nodesCopy),
            };

            Fn fn = (cv) =>
            {
                for (int i = 0; i + replicas[cv] < selectors[cv]; i += replicas[cv])
                {
                    var addrs = tr.Next();
                    Assert.AreEqual(replicas[cv], addrs.Length);
                    for (int j = 0; j < addrs.Length; j++)
                    {
                        Assert.AreEqual(nodes[cv][i + j].NetworkAddress, addrs[j].String());
                    }
                }

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
    }
}
