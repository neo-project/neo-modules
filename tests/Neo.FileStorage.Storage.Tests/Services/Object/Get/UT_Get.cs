using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.LocalObjectStorage;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Get;
using Neo.FileStorage.Storage.Services.Object.Get.Remote;
using Neo.FileStorage.Storage.Services.Object.Get.Writer;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Utils;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.Tests.Sercies.Object.Get
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
                    throw new Storage.LocalObjectStorage.SplitInfoException(si);
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

        private class TestEpochSource : IEpochSource
        {
            public ulong Epoch = 0;
            public ulong CurrentEpoch => Epoch;
        }

        private class TestPlacementBuilder : IPlacementBuilder
        {
            public Dictionary<ContainerID, List<List<Node>>> Vectors = new();

            public List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy)
            {
                if (Vectors.TryGetValue(address.ContainerId, out var vector))
                {
                    return vector;
                }
                throw new InvalidOperationException("vectors for address not found");
            }
        }

        private class TestTraverserGenerator : ITraverserGenerator
        {
            public FSContainer Container;
            public Dictionary<ulong, IPlacementBuilder> B = new();

            public Traverser GenerateTraverser(Address address, ulong epoch)
            {
                return new Traverser(B[epoch], Container.PlacementPolicy, address, trackCopies: false);
            }
        }

        private class TestClientCache : IGetClientCache
        {
            public Dictionary<string, IGetClient> clients = new();

            public IGetClient Get(List<Network.Address> addresses)
            {
                var key = string.Join("", addresses.Select(p => p.ToString()));
                if (clients.TryGetValue(key, out var client))
                    return client;
                throw new InvalidOperationException();
            }
        }

        private class TestObjectClient : IGetClient
        {
            public Dictionary<Address, FSObject> Results = new();

            public IRawObjectClient Raw()
            {
                return this;
            }

            public Task<Address> DeleteObject(Address address, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<FSObject> GetObject(Address address, bool raw = false, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<FSObject> GetObjectHeader(Address address, bool minimal = false, bool raw = false, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<byte[]> GetObjectPayloadRangeData(Address address, FSRange range, bool raw = false, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<FSRange> ranges, ChecksumType type, byte[] salt, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, CallOptions options = null, CancellationToken context = default)
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

            public Task<PutStream> PutObject(PutRequest init, DateTime? deadline = null, CancellationToken context = default)
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
                KeyStorage = new(null, new()),
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
    }
}
