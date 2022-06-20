using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Search;
using Neo.FileStorage.Storage.Services.Object.Search.Execute;
using Neo.FileStorage.Storage.Services.Object.Search.Remote;
using Neo.FileStorage.Storage.Services.Object.Search.Writer;
using Neo.FileStorage.Storage.Services.Object.Util;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Search
{
    [TestClass]
    public class UT_Search
    {
        private class TestStorage : ILocalSearchSource, ISearchClient
        {
            private readonly Dictionary<ContainerID, List<ObjectID>> items = new();

            public List<Address> Select(ContainerID cid, SearchFilters filters)
            {
                if (items.TryGetValue(cid, out var res))
                    return res?.Select(p => new Address { ContainerId = cid, ObjectId = p }).ToList();
                return new();
            }

            public IEnumerable<ObjectID> SearchObjects(ExecuteContext context)
            {
                if (items.TryGetValue(context.Prm.ContainerID, out var value))
                {
                    return value;
                }
                return new List<ObjectID>();
            }

            public void AddResult(ContainerID cid, List<ObjectID> ids)
            {
                items[cid] = ids;
            }
        }

        private class TestPlacementBuilder : IPlacementBuilder
        {
            public Dictionary<ContainerID, List<List<Node>>> Vectors = new();

            public List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy)
            {
                if (Vectors.TryGetValue(address.ContainerId, out var vector))
                {
                    List<List<Node>> res = new();
                    foreach (var list in vector)
                        res.Add(list.ToList());
                    return res;
                }
                throw new InvalidOperationException("vectors for address not found");
            }
        }

        private class TestTraverserGenerator : ITraverserGenerator
        {
            public FSContainer Container;
            public Dictionary<ulong, TestPlacementBuilder> B = new();

            public Traverser GenerateTraverser(Address address, ulong epoch)
            {
                return new Traverser(B[epoch], Container.PlacementPolicy, address, trackCopies: false);
            }
        }

        private class TestClientCache : ISearchClientCache
        {
            public Dictionary<string, TestStorage> Clients = new();

            public ISearchClient Get(NodeInfo node)
            {
                if (Clients.TryGetValue(string.Join("", node.Addresses.Select(p => p.ToString())), out var value))
                {
                    return value;
                }
                throw new InvalidOperationException("could not contruct client");
            }
        }

        [TestMethod]
        public void TestGetLocallyOk()
        {
            var storage = new TestStorage();
            var srv = new SearchService
            {
                LocalStorage = storage,
            };
            var cid = RandomContainerID();
            var ids = RandomObjectIDs(10);
            storage.AddResult(cid, ids);
            var w = new SimpleIDWriter();
            var prm = new SearchPrm
            {
                Local = true,
                ContainerID = cid,
                Writer = w,
            };
            srv.Search(prm, default);
            Assert.AreEqual(10, w.IDs.Count);
        }

        [TestMethod]
        public void TestGetLocallyFail()
        {
            var storage = new TestStorage();
            var srv = new SearchService
            {
                LocalStorage = storage,
            };
            var cid = RandomContainerID();
            storage.AddResult(cid, null);
            var w = new SimpleIDWriter();
            var prm = new SearchPrm
            {
                Local = true,
                ContainerID = cid,
                Writer = w,
            };
            Assert.ThrowsException<ArgumentNullException>(() => srv.Search(prm, default));
        }

        [TestMethod]
        public void TestRemoteSmall()
        {
            var container = new FSContainer();
            var pp = new PlacementPolicy(0, new Replica[] { new Replica(2, "") }, null, null);
            container.PlacementPolicy = pp;
            var cid = container.CalCulateAndGetId;
            var address = RandomAddress(cid);
            TestNodeMatrix(new int[] { 2 }, out var ns, out _);
            var builder = new TestPlacementBuilder();
            builder.Vectors = new() { { cid, ns } };
            var c1 = new TestStorage();
            var ids1 = RandomObjectIDs(10);
            c1.AddResult(cid, ids1);
            var c2 = new TestStorage();
            var ids2 = RandomObjectIDs(10);
            c2.AddResult(cid, ids2);
            const ulong epoch = 13;
            Console.WriteLine($"{ns[0][0].Addresses[0]}, {ns[0][1].Addresses[0]}");
            var srv = new SearchService
            {
                LocalStorage = new TestStorage(),
                TraverserGenerator = new TestTraverserGenerator
                {
                    Container = container,
                    B = new() { { epoch, builder } },
                },
                EpochSource = new TestEpochSource { Epoch = epoch },
                ClientCache = new TestClientCache
                {
                    Clients = new()
                    {
                        { ns[0][0].Addresses[0], c1 },
                        { ns[0][1].Addresses[0], c2 }
                    }
                }
            };
            var w = new SimpleIDWriter();
            var prm = new SearchPrm
            {
                Local = false,
                ContainerID = cid,
                Writer = w,
            };
            srv.Search(prm, default);
            Assert.AreEqual(ids1.Count + ids2.Count, w.IDs.Count);
        }
    }
}
