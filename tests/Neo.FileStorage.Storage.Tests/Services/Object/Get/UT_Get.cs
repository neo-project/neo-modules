using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services;
using Neo.FileStorage.Storage.Services.Object.Get;
using Neo.FileStorage.Storage.Services.Object.Get.Remote;
using Neo.FileStorage.Storage.Services.Object.Get.Writer;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Session.Storage;
using static Neo.FileStorage.Storage.Helper;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Get
{
    [TestClass]
    public class UT_Get
    {
        private class TestLocalObjectSource : ILocalObjectSource
        {
            public List<Address> Inhumed = new();
            public Dictionary<Address, SplitInfo> Virtual = new();
            public Dictionary<Address, FSObject> Phy = new();

            public FSObject Get(Address address)
            {
                if (Inhumed.Contains(address))
                    throw new ObjectAlreadyRemovedException();
                if (Virtual.TryGetValue(address, out var si))
                    throw new SplitInfoException(si);
                if (Phy.TryGetValue(address, out var obj))
                    return obj;

                throw new ObjectNotFoundException();
            }

            public FSObject GetRange(Address address, FSRange range)
            {
                var obj = Get(address);
                return new() { Payload = obj.Payload.Range(range.Offset, range.Offset + range.Length) };
            }

            public FSObject Head(Address address, bool raw)
            {
                return Get(address).CutPayload();
            }
        }

        private class TestTraverserGenerator : ITraverserGenerator
        {
            public FSContainer Container;
            public Dictionary<ulong, IPlacementBuilder> Builders = new();

            public Traverser GenerateTraverser(Address address, ulong epoch)
            {
                return new Traverser(Builders[epoch], Container.PlacementPolicy, address, 1, false);
            }
        }

        private class TestClientCache : IGetClientCache
        {
            public Dictionary<string, IGetClient> Clients = new();

            public IGetClient Get(NodeInfo node)
            {
                var key = string.Join("", node.Addresses.Select(p => p.ToString()));
                if (Clients.TryGetValue(key, out var client))
                    return client;
                throw new InvalidOperationException();
            }
        }

        private class TestObjectClient : IGetClient, IObjectGetClient, IRawObjectGetClient
        {
            public Dictionary<Address, (FSObject, object)> Results = new();

            public IRawObjectGetClient RawObjectGetClient()
            {
                return this;
            }

            public Task<Address> DeleteObject(Address address, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<FSObject> GetObject(Address address, bool raw = false, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                return Task.Run(() =>
                {
                    if (Results.TryGetValue(address, out var item))
                    {
                        if (item.Item2 is not null && item.Item2 is string error) throw new RpcException(new(StatusCode.Unknown, error));
                        if (item.Item2 is not null && item.Item2 is SplitInfo si) throw new SplitInfoException(si);
                        return item.Item1;
                    }
                    throw new RpcException(new(StatusCode.Unknown, "object not found"));
                });
            }

            public Task<FSObject> GetObjectHeader(Address address, bool minimal = false, bool raw = false, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                return Task.Run(() =>
                {
                    if (Results.TryGetValue(address, out var item))
                    {
                        if (item.Item2 is not null && item.Item2 is string error) throw new RpcException(new(StatusCode.Unknown, error));
                        if (item.Item2 is not null && item.Item2 is SplitInfo si) throw new SplitInfoException(si);
                        return item.Item1.CutPayload();
                    }
                    throw new RpcException(new(StatusCode.Unknown, "object not found"));
                });
            }

            public Task<byte[]> GetObjectPayloadRangeData(Address address, FSRange range, bool raw = false, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                return Task.Run(() =>
                {
                    if (Results.TryGetValue(address, out var item))
                    {
                        if (item.Item2 is not null && item.Item2 is string error) throw new RpcException(new(StatusCode.Unknown, error));
                        if (item.Item2 is not null && item.Item2 is SplitInfo si) throw new SplitInfoException(si);
                        return item.Item1.Payload.Range(range.Offset, range.Offset + range.Length).ToByteArray();
                    }
                    throw new RpcException(new(StatusCode.Unknown, "object not found"));
                });
            }

            public Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<FSRange> ranges, ChecksumType type, byte[] salt, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<ObjectID> PutObject(FSObject obj, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, API.Client.CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<Address> DeleteObject(DeleteRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<FSObject> GetObject(GetRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<FSObject> GetObjectHeader(HeadRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<byte[]> GetObjectPayloadRangeData(GetRangeRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<byte[]>> GetObjectPayloadRangeHash(GetRangeHashRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<IClientStream> PutObject(PutRequest init, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<ObjectID>> SearchObject(SearchRequest request, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod]
        public void TestGetLocalOnly()
        {
            TestLocalObjectSource storage = new();
            GetService getService = new()
            {
                Assemble = true,
                KeyStore = new KeyStore(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
            };
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Local = true,
                Raw = false,
                Writer = writer,
            };
            var obj = RandomObject(30);
            var address = obj.Address;
            storage.Phy.Add(address, obj);
            gprm.Address = address;
            using CancellationTokenSource source = new();
            getService.Get(gprm, source.Token);
            Assert.IsTrue(writer.Object.Equals(obj));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                }
            };
            getService.GetRange(rprm, source.Token);
            var data = writer.Object.Payload.ToByteArray();
            Assert.AreEqual(obj.Payload.ToByteArray().Skip(obj.Payload.Length / 3).Take(obj.Payload.Length / 3).ToArray().ToHexString(), data.ToHexString());
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
            };
            getService.Head(hprm, source.Token);
            Assert.IsTrue(writer.Object.Equals(obj.CutPayload()));
        }

        [TestMethod]
        public void TestGetLocalOnlyInhumed()
        {
            TestLocalObjectSource storage = new();
            GetService getService = new()
            {
                Assemble = true,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
            };
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Local = true,
                Raw = false,
                Writer = writer,
            };
            var obj = RandomObject(30);
            var address = obj.Address;
            storage.Inhumed.Add(address);
            gprm.Address = address;
            using CancellationTokenSource source = new();
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.Get(gprm, source.Token));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                }
            };
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.GetRange(rprm, source.Token));
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
            };
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.Head(hprm, source.Token));
        }

        [TestMethod]
        public void TestGetLocalOnly404()
        {
            TestLocalObjectSource storage = new();
            GetService getService = new()
            {
                Assemble = true,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
            };
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Local = true,
                Raw = false,
                Writer = writer,
            };
            var obj = RandomObject(30);
            var address = obj.Address;
            gprm.Address = address;
            using CancellationTokenSource source = new();
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, source.Token));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                }
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, source.Token));
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = true,
                Raw = false,
                Writer = writer,
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Head(hprm, source.Token));
        }

        private void GenerateChain(int len, ContainerID cid, out List<FSObject> objects, out List<ObjectID> objectIds, out byte[] payload)
        {
            objects = new();
            objectIds = new();
            payload = Array.Empty<byte>();
            ObjectID prevId = null;
            for (int i = 0; i < len; i++)
            {
                var o = RandomObject(cid, 10);
                o.Header.Split = new()
                {
                    Previous = prevId,
                };
                objects.Add(o);
                objectIds.Add(o.ObjectId);
                payload = payload.Concat(o.Payload.ToByteArray()).ToArray();
                prevId = o.ObjectId;
            }
        }

        [TestMethod]
        public void TestRemoteSmall()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            var cid = RandomContainerID();
            var obj = RandomObject(cid, 10);
            var address = obj.Address;
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            builder.Vectors[address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            TestObjectClient c1 = new();
            c1.Results[address] = (obj, null);
            TestObjectClient c2 = new();
            c2.Results[address] = (null, "");
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            getService.Get(gprm, default);
            Assert.AreEqual(obj.ToString(), writer.Object.ToString());
            c1.Results[address] = (null, "");
            c2.Results[address] = (obj, null);
            getService.Get(gprm, default);
            Assert.AreEqual(obj.ToString(), writer.Object.ToString());
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                },
                CallOptions = new(),
            };
            getService.GetRange(rprm, default);
            Assert.AreEqual(obj.Payload.Range((ulong)obj.Payload.Length / 3, (ulong)obj.Payload.Length / 3 + (ulong)obj.Payload.Length / 3).ToByteArray().ToHexString(), writer.Object.Payload.ToByteArray().ToHexString());
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            getService.Head(hprm, default);
            Assert.AreEqual(obj.CutPayload().ToString(), writer.Object.ToString());
        }

        [TestMethod]
        public void TestRemoteSmallInhume()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            var cid = RandomContainerID();
            var obj = RandomObject(cid, 10);
            var address = obj.Address;
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            builder.Vectors[address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            TestObjectClient c1 = new();
            c1.Results[address] = (null, "");
            TestObjectClient c2 = new();
            c2.Results[address] = (null, ObjectException.AlreadyRemovedError);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.GetRange(rprm, default));
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectAlreadyRemovedException>(() => getService.Head(hprm, default));
        }

        [TestMethod]
        public void TestRemoteSmall404()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            var cid = RandomContainerID();
            var obj = RandomObject(cid, 10);
            var address = obj.Address;
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            builder.Vectors[address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            TestObjectClient c1 = new();
            c1.Results[address] = (null, ObjectException.NotFoundError);
            TestObjectClient c2 = new();
            c2.Results[address] = (null, ObjectException.NotFoundError);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = (ulong)obj.Payload.Length / 3,
                    Length = (ulong)obj.Payload.Length / 3,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, default));
            writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Head(hprm, default));
        }

        [TestMethod]
        public void TestRemoteSmallVirtualFailing()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            var cid = RandomContainerID();
            var oid = RandomObjectID();
            var address = new Address()
            {
                ContainerId = cid,
                ObjectId = oid,
            };
            var link = RandomObjectID();
            var splitAddress = new Address()
            {
                ContainerId = cid,
                ObjectId = link,
            };
            var si = new SplitInfo
            {
                Link = link,
            };
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            builder.Vectors[address] = nss;
            builder.Vectors[splitAddress] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            TestObjectClient c1 = new();
            c1.Results[address] = (null, ObjectException.NotFoundError);
            c1.Results[splitAddress] = (null, ObjectException.NotFoundError);
            TestObjectClient c2 = new();
            c2.Results[address] = (null, ObjectException.NotFoundError);
            c2.Results[splitAddress] = (null, si);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = splitAddress,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = 0,
                    Length = 0,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, default));
        }

        [TestMethod]
        public void TestRemoteSmallVirtualElementFailure()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            TestObjectClient c2 = new();
            TestObjectClient c1 = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);

            var cid = RandomContainerID();
            var srcObj = RandomObject(cid, 10);
            var address = srcObj.Address;
            var link = RandomObjectID();
            var splitInfo = new SplitInfo
            {
                Link = link,
            };
            GenerateChain(2, cid, out var objs, out var oids, out var payload);
            var linkAddress = new Address
            {
                ContainerId = cid,
                ObjectId = link,
            };
            var linkObj = RandomObject(cid, 10);
            linkObj.Children = oids;
            linkObj.ObjectId = link;
            linkObj.Parent = srcObj;
            var child1Address = new Address
            {
                ContainerId = cid,
                ObjectId = oids[0],
            };
            var child2Address = new Address
            {
                ContainerId = cid,
                ObjectId = oids[1],
            };

            builder.Vectors[address] = nss;
            builder.Vectors[linkAddress] = nss;
            builder.Vectors[child1Address] = nss;
            builder.Vectors[child2Address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            c1.Results[address] = (null, "any error");
            c1.Results[linkAddress] = (null, "any error");
            c1.Results[child1Address] = (null, "any error");
            c1.Results[child2Address] = (null, "any error");
            c2.Results[address] = (null, splitInfo);
            c2.Results[linkAddress] = (linkObj, null);
            c2.Results[child1Address] = (objs[0], null);
            c2.Results[child2Address] = (null, ObjectException.NotFoundError);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = 0,
                    Length = 1,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, default));
        }

        [TestMethod]
        public void TestRemoteSmallVirtualOK()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            TestObjectClient c2 = new();
            TestObjectClient c1 = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);

            var cid = RandomContainerID();
            var srcObj = RandomObject(cid, 10);
            var address = srcObj.Address;
            var link = RandomObjectID();
            var splitInfo = new SplitInfo
            {
                Link = link,
            };
            GenerateChain(2, cid, out var children, out var oids, out var payload);
            srcObj.Header.PayloadLength = (ulong)payload.Length;
            srcObj.Payload = ByteString.CopyFrom(payload);
            children[^1].Parent = srcObj;
            var linkAddress = new Address
            {
                ContainerId = cid,
                ObjectId = link,
            };
            var linkObj = RandomObject(cid, 0);
            linkObj.Children = oids;
            linkObj.ObjectId = link;
            linkObj.Parent = srcObj;
            var child1Address = new Address
            {
                ContainerId = cid,
                ObjectId = oids[0],
            };
            var child2Address = new Address
            {
                ContainerId = cid,
                ObjectId = oids[1],
            };

            builder.Vectors[address] = nss;
            builder.Vectors[linkAddress] = nss;
            builder.Vectors[child1Address] = nss;
            builder.Vectors[child2Address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            c1.Results[address] = (null, "any error");
            c1.Results[linkAddress] = (null, "any error");
            c1.Results[child1Address] = (null, "any error");
            c1.Results[child2Address] = (null, "any error");
            c2.Results[address] = (null, splitInfo);
            c2.Results[linkAddress] = (linkObj, null);
            c2.Results[child1Address] = (children[0], null);
            c2.Results[child2Address] = (children[1], null);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            getService.Get(gprm, default);
            var obj = writer.Object;
            Assert.AreEqual(srcObj.ToString(), obj.ToString());
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = srcObj.PayloadSize / 3,
                    Length = srcObj.PayloadSize / 3,
                },
                CallOptions = new(),
            };
            getService.GetRange(rprm, default);
            Assert.AreEqual(srcObj.Payload.Range(srcObj.PayloadSize / 3, srcObj.PayloadSize / 3 + (ulong)srcObj.Payload.Length / 3).ToByteArray().ToHexString(), writer.Object.Payload.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void TestRemoteSmallVirtualRightFailure()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            TestObjectClient c2 = new();
            TestObjectClient c1 = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);

            var cid = RandomContainerID();
            var address = RandomAddress(cid);
            var last = RandomObjectID();
            var splitInfo = new SplitInfo
            {
                LastPart = last,
            };
            var splitAddress = new Address
            {
                ContainerId = cid,
                ObjectId = last,
            };

            builder.Vectors[address] = nss;
            builder.Vectors[splitAddress] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            c1.Results[address] = (null, "any error");
            c1.Results[splitAddress] = (null, ObjectException.NotFoundError);
            c2.Results[address] = (null, splitInfo);
            c2.Results[splitAddress] = (null, ObjectException.NotFoundError);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = 0,
                    Length = 0,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, default));
        }

        [TestMethod]
        public void TestRemoteSmallVirtualRightElementFailure()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            TestObjectClient c2 = new();
            TestObjectClient c1 = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);

            var cid = RandomContainerID();
            var srcObj = RandomObject(cid, 10);
            var address = srcObj.Address;
            var last = RandomObjectID();
            var splitInfo = new SplitInfo
            {
                LastPart = last,
            };
            var rightAddress = new Address
            {
                ContainerId = cid,
                ObjectId = last,
            };
            GenerateChain(2, cid, out var children, out var oids, out var payload);
            var rightObj = children[^1];
            rightObj.Parent = srcObj;
            var preRightAddress = children[^2].Address;

            builder.Vectors[address] = nss;
            builder.Vectors[rightAddress] = nss;
            builder.Vectors[preRightAddress] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            c1.Results[address] = (null, "any error");
            c1.Results[rightAddress] = (null, "any error");
            c2.Results[address] = (null, splitInfo);
            c2.Results[rightAddress] = (rightObj, null);
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = 0,
                    Length = 1,
                },
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.GetRange(rprm, default));
        }

        [TestMethod]
        public void TestRemoteSmallVirtualRightOk()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            TestObjectClient c2 = new();
            TestObjectClient c1 = new();
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);

            var cid = RandomContainerID();
            var srcObj = RandomObject(cid, 10);
            var address = srcObj.Address;
            var last = RandomObjectID();
            var splitInfo = new SplitInfo
            {
                LastPart = last,
            };
            var rightAddress = new Address
            {
                ContainerId = cid,
                ObjectId = last,
            };
            GenerateChain(2, cid, out var children, out var oids, out var payload);
            srcObj.Header.PayloadLength = (ulong)payload.Length;
            srcObj.Payload = ByteString.CopyFrom(payload);
            var rightObj = children[^1];
            rightObj.Parent = srcObj;
            rightObj.ObjectId = last;
            var preRightAddress = children[^2].Address;

            builder.Vectors[address] = nss;
            generator.Container = cnr;
            generator.Builders[epoch] = builder;
            c1.Results[address] = (null, "any error");
            c2.Results[address] = (null, splitInfo);
            foreach (var child in children)
            {
                c1.Results[child.Address] = (null, "any error");
                c2.Results[child.Address] = (child, null);
                builder.Vectors[child.Address] = nss;
            }
            clientCache.Clients[addrss[0][0]] = c1;
            clientCache.Clients[addrss[0][1]] = c2;
            SimpleObjectWriter writer = new();
            HeadPrm hprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<SplitInfoException>(() => getService.Head(hprm, default));
            writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            getService.Get(gprm, default);
            Assert.AreEqual(srcObj.ToString(), writer.Object.ToString());
            writer = new();
            RangePrm rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = srcObj.PayloadSize / 3,
                    Length = srcObj.PayloadSize / 3,
                },
                CallOptions = new(),
            };
            getService.GetRange(rprm, default);
            Assert.AreEqual(srcObj.Payload.Range(srcObj.PayloadSize / 3, srcObj.PayloadSize / 3 + (ulong)srcObj.Payload.Length / 3).ToByteArray().ToHexString(), writer.Object.Payload.ToByteArray().ToHexString());
            writer = new();
            rprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                Range = new()
                {
                    Offset = srcObj.PayloadSize - 2,
                    Length = 1,
                },
                CallOptions = new(),
            };
            getService.GetRange(rprm, default);
            Assert.AreEqual(srcObj.Payload.Range(srcObj.PayloadSize - 2, srcObj.PayloadSize - 1).ToByteArray().ToHexString(), writer.Object.Payload.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void TestFromPastEpoch()
        {
            const ulong epoch = 13;
            TestLocalObjectSource storage = new();
            TestTraverserGenerator generator = new();
            TestPlacementBuilder builder1 = new();
            TestPlacementBuilder builder2 = new();
            TestEpochSource epochSource = new();
            TestClientCache clientCache = new();
            GetService getService = new()
            {
                Assemble = true,
                EpochSource = epochSource,
                KeyStore = new(null, new TokenStore(new TestDB()), null),
                LocalStorage = storage,
                ClientCache = clientCache,
                TraverserGenerator = generator,
            };
            FSContainer cnr = new()
            {
                PlacementPolicy = new(),
            };
            var cid = RandomContainerID();
            var obj = RandomObject(cid);
            var address = obj.Address;
            TestNodeMatrix(new int[] { 2, 2 }, out var nss, out var addrss);
            TestObjectClient c11 = new();
            c11.Results[address] = (null, ObjectException.NotFoundError);
            TestObjectClient c12 = new();
            c12.Results[address] = (null, ObjectException.NotFoundError);
            TestObjectClient c21 = new();
            c21.Results[address] = (null, ObjectException.NotFoundError);
            TestObjectClient c22 = new();
            c22.Results[address] = (obj, null);
            builder1.Vectors[address] = new() { nss[0] };
            builder2.Vectors[address] = new() { nss[1] };
            generator.Container = cnr;
            generator.Builders[epoch - 1] = builder2;
            generator.Builders[epoch] = builder1;
            clientCache.Clients[addrss[0][0]] = c11;
            clientCache.Clients[addrss[0][1]] = c12;
            clientCache.Clients[addrss[1][0]] = c21;
            clientCache.Clients[addrss[1][1]] = c22;
            epochSource.Epoch = epoch;
            SimpleObjectWriter writer = new();
            GetPrm gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                CallOptions = new(),
            };
            Assert.ThrowsException<ObjectNotFoundException>(() => getService.Get(gprm, default));
            gprm = new()
            {
                Address = address,
                Local = false,
                Raw = false,
                Writer = writer,
                NetmapLookupDepth = 1,
                CallOptions = new(),
            };
            getService.Get(gprm, default);
            Assert.AreEqual(obj.ToString(), writer.Object.ToString());
        }
    }
}
