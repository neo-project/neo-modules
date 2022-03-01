using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Session.Storage;
using Neo.FileStorage.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Neo.FileStorage.Storage.Helper;
using static Neo.FileStorage.Storage.Tests.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_PutService : TestKit
    {
        private class TestMaxObjectSizeSource : IMaxObjectSizeSource
        {
            public ulong MaxSize = 1024;

            public ulong MaxObjectSize()
            {
                return MaxSize;
            }
        }

        private class TestNetmapSource : INetmapSource
        {
            public ulong CurrentEpoch;
            public Dictionary<ulong, NetMap> NetMaps = new();

            public NetMap GetNetMapByDiff(ulong diff)
            {
                if (NetMaps.TryGetValue(CurrentEpoch - diff, out var nm))
                    return nm;
                throw new InvalidOperationException("could not get netmap");
            }

            public NetMap GetNetMapByEpoch(ulong epoch)
            {
                if (NetMaps.TryGetValue(epoch, out var nm))
                    return nm;
                throw new InvalidOperationException("could not get netmap");
            }
        }

        private class TestLocalObjectStore : ILocalObjectStore
        {
            public FSObject Object;

            public void Put(FSObject obj)
            {
                Object = obj;
            }
        }

        private class TestPutStream : IClientStream
        {
            public FSObject Object;

            public Task Write(IRequest request)
            {
                return Task.Run(() =>
                {
                    if (request is API.Object.PutRequest putRequest)
                    {
                        if (putRequest.Body.ObjectPartCase == API.Object.PutRequest.Types.Body.ObjectPartOneofCase.Chunk)
                        {
                            Object.Payload = Object.Payload.Concat(putRequest.Body.Chunk);
                            return;
                        }
                    }
                    throw new InvalidOperationException("invalid request");
                });
            }

            public Task<IResponse> Close()
            {
                return Task.Run(() =>
                {
                    return (IResponse)new API.Object.PutResponse
                    {
                        Body = new()
                        {
                            ObjectId = Object.ObjectId
                        }
                    };
                });
            }

            public void Dispose() { }
        }

        private class TestPutClient : IPutClient, IObjectPutClient, IRawObjectPutClient
        {
            public FSObject Object = new();

            public IRawObjectPutClient RawObjectPutClient()
            {
                return this;
            }

            public Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken context = default)
            {
                Object = obj;
                return Task.Run(() => obj.ObjectId);
            }

            public Task<IClientStream> PutObject(API.Object.PutRequest init, DateTime? deadline = null, CancellationToken context = default)
            {
                return Task.Run(() =>
                {
                    Object.Header = init.Body.Init.Header;
                    Object.ObjectId = init.Body.Init.ObjectId;
                    Object.Signature = init.Body.Init.Signature;
                    return (IClientStream)new TestPutStream
                    {
                        Object = Object,
                    };
                });
            }
        }

        private class TestPutClientCache : IPutClientCache
        {
            public Dictionary<string, TestPutClient> Clients = new();

            public IPutClient Get(NodeInfo node)
            {
                lock (Clients)
                {
                    var key = string.Join("", node.Addresses.Select(p => p.ToString()));
                    Clients[key] = new TestPutClient();
                    return Clients[key];
                }
            }
        }

        private List<API.Object.PutRequest> MakeRequests(FSObject obj, uint ttl = 1, int chunkSize = 512)
        {
            List<API.Object.PutRequest> requests = new();
            RequestMetaHeader meta = new();
            meta.Ttl = ttl;
            API.Object.PutRequest init = new()
            {
                MetaHeader = meta,
                Body = new()
                {
                    Init = new()
                    {
                        ObjectId = obj.ObjectId,
                        Header = obj.Header,
                        Signature = obj.Signature,
                    }
                }
            };
            requests.Add(init);
            int offset = 0;
            var payload = obj.Payload.ToByteArray();
            while (offset < obj.Payload.Length)
            {
                int end = offset + chunkSize;
                if (obj.Payload.Length < end)
                    end = obj.Payload.Length;
                API.Object.PutRequest req = new()
                {
                    MetaHeader = meta,
                    Body = new()
                    {
                        Chunk = ByteString.CopyFrom(payload[offset..end]),
                    }
                };
                offset = end;
                requests.Add(req);
            }
            return requests;
        }

        [TestMethod]
        public void TestCompleteObjectLocal()
        {
            const ulong epoch = 13;
            var key = RandomPrivatekey().LoadPrivateKey();
            TestMaxObjectSizeSource maxObejectSizeSource = new();
            maxObejectSizeSource.MaxSize = 1024;
            TestContainerSource containerSource = new();
            TestNetmapSource netmapSource = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestLocalObjectStore localStore = new();
            TokenStore ts = new(new TestDB());
            KeyStore ks = new(key, ts, null);
            TestPutClientCache clientCache = new();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_PutService), 10));
            var service = new PutService
            {
                MaxObjectSizeSource = maxObejectSizeSource,
                ContainerSoruce = containerSource,
                NetmapSource = netmapSource,
                EpochSource = epochSource,
                LocalInfo = localInfo,
                KeyStorage = ks,
                LocalObjectStore = localStore,
                ObjectInhumer = null,
                ClientCache = clientCache,
                LocalPool = work_pool,
                RemotePool = work_pool
            };
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(1, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new ContainerWithSignature
            {
                Container = container,
            };
            NodeInfo ni = new();
            ni.PublicKey = ByteString.CopyFrom(key.PublicKey());
            NetMap nm = new(new List<Node>() { new Node(0, ni) });
            netmapSource.NetMaps[epoch] = nm;
            netmapSource.CurrentEpoch = epoch;
            var obj = RandomObject(cid, 1024);
            var reqs = MakeRequests(obj);
            using CancellationTokenSource source = new();
            using var stream = service.Put(source.Token);
            foreach (var req in reqs)
                stream.Send(req);
            var r = (API.Object.PutResponse)stream.Close();
            Assert.IsTrue(r.Body.ObjectId.Equals(obj.ObjectId));
            Assert.AreEqual(obj.ToString(), localStore.Object.ToString());
        }

        [TestMethod]
        public void TestCompleteObjectRemote()
        {
            const ulong epoch = 13;
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            var key = RandomPrivatekey().LoadPrivateKey();
            TestMaxObjectSizeSource maxObejectSizeSource = new();
            maxObejectSizeSource.MaxSize = 1024;
            TestContainerSource containerSource = new();
            TestNetmapSource netmapSource = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestLocalObjectStore localStore = new();
            TokenStore ts = new(new TestDB());
            KeyStore ks = new(key, ts, null);
            TestPutClientCache clientCache = new();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_PutService), 10));
            var service = new PutService
            {
                MaxObjectSizeSource = maxObejectSizeSource,
                ContainerSoruce = containerSource,
                NetmapSource = netmapSource,
                EpochSource = epochSource,
                LocalInfo = localInfo,
                KeyStorage = ks,
                LocalObjectStore = localStore,
                ObjectInhumer = null,
                ClientCache = clientCache,
                LocalPool = work_pool,
                RemotePool = work_pool
            };
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(2, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new ContainerWithSignature
            {
                Container = container,
            };
            NetMap nm = new(nss.Flatten());
            netmapSource.NetMaps[epoch] = nm;
            netmapSource.CurrentEpoch = epoch;
            var obj = RandomObject(cid, 1024);
            var reqs = MakeRequests(obj, 2);
            using CancellationTokenSource source = new();
            using var stream = service.Put(source.Token);
            foreach (var req in reqs)
                stream.Send(req);
            var r = (API.Object.PutResponse)stream.Close();
            Assert.IsTrue(r.Body.ObjectId.Equals(obj.ObjectId));
            Assert.AreEqual(2, clientCache.Clients.Count);
            Assert.AreEqual(clientCache.Clients[addrss[0][0]].Object.ToString(), obj.ToString());
        }

        [TestMethod]
        public void TestIncompleteObjectLocal()
        {
            const ulong epoch = 13;
            var key = RandomPrivatekey().LoadPrivateKey();
            TestMaxObjectSizeSource maxObejectSizeSource = new();
            maxObejectSizeSource.MaxSize = 1024;
            TestContainerSource containerSource = new();
            TestNetmapSource netmapSource = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestLocalObjectStore localStore = new();
            TokenStore ts = new(new TestDB());
            KeyStore ks = new(key, ts, null);
            TestPutClientCache clientCache = new();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_PutService), 10));
            var service = new PutService
            {
                MaxObjectSizeSource = maxObejectSizeSource,
                ContainerSoruce = containerSource,
                NetmapSource = netmapSource,
                EpochSource = epochSource,
                LocalInfo = localInfo,
                KeyStorage = ks,
                LocalObjectStore = localStore,
                ObjectInhumer = null,
                ClientCache = clientCache,
                LocalPool = work_pool,
                RemotePool = work_pool
            };
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(1, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new ContainerWithSignature
            {
                Container = container,
            };
            NodeInfo ni = new();
            ni.PublicKey = ByteString.CopyFrom(key.PublicKey());
            NetMap nm = new(new List<Node>() { new Node(0, ni) });
            netmapSource.NetMaps[epoch] = nm;
            netmapSource.CurrentEpoch = epoch;
            Random random = new();
            var payload = new byte[1024];
            random.NextBytes(payload);
            FSObject obj = new()
            {
                Header = new()
                {
                    ContainerId = cid,
                    OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                },
                Payload = ByteString.CopyFrom(payload),
            };
            var reqs = MakeRequests(obj);
            using CancellationTokenSource source = new();
            using var stream = service.Put(source.Token);
            foreach (var req in reqs)
                stream.Send(req);
            var r = (API.Object.PutResponse)stream.Close();
            Assert.IsTrue(obj.Header.ContainerId.Equals(localStore.Object.Header.ContainerId));
            Assert.IsTrue(obj.Header.OwnerId.Equals(localStore.Object.Header.OwnerId));
            Assert.IsTrue(obj.Payload.Equals(localStore.Object.Payload));
            Assert.IsTrue(localStore.Object.CheckVerificationFields());
        }

        [TestMethod]
        public void TestIncompleteObjectRemote()
        {
            const ulong epoch = 13;
            var localAddr = Network.Address.FromString("/ip4/0.0.0.0/tcp/8080");
            TestNodeMatrix(new int[] { 2 }, out var nss, out var addrss);
            var key = RandomPrivatekey().LoadPrivateKey();
            TestMaxObjectSizeSource maxObejectSizeSource = new();
            maxObejectSizeSource.MaxSize = 1024;
            TestContainerSource containerSource = new();
            TestNetmapSource netmapSource = new();
            TestEpochSource epochSource = new();
            epochSource.Epoch = epoch;
            TestLocalInfo localInfo = new();
            localInfo.PublicKey = key.PublicKey();
            TestLocalObjectStore localStore = new();
            TokenStore ts = new(new TestDB());
            KeyStore ks = new(key, ts, null);
            TestPutClientCache clientCache = new();
            var work_pool = Sys.ActorOf(WorkerPool.Props(nameof(UT_PutService), 10));
            var service = new PutService
            {
                MaxObjectSizeSource = maxObejectSizeSource,
                ContainerSoruce = containerSource,
                NetmapSource = netmapSource,
                EpochSource = epochSource,
                LocalInfo = localInfo,
                KeyStorage = ks,
                LocalObjectStore = localStore,
                ObjectInhumer = null,
                ClientCache = clientCache,
                LocalPool = work_pool,
                RemotePool = work_pool
            };
            var container = new Container
            {
                Version = API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = BasicAcl.PublicBasicRule,
                PlacementPolicy = new(1, new Replica[] { new Replica(2, "") }, null, null),
            };
            var cid = container.CalCulateAndGetId;
            containerSource.Containers[cid] = new ContainerWithSignature
            {
                Container = container,
            };
            NetMap nm = new(nss.Flatten());
            netmapSource.NetMaps[epoch] = nm;
            netmapSource.CurrentEpoch = epoch;
            Random random = new();
            var payload = new byte[1024];
            random.NextBytes(payload);
            FSObject obj = new()
            {
                Header = new()
                {
                    ContainerId = cid,
                    OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                },
                Payload = ByteString.CopyFrom(payload),
            };
            var reqs = MakeRequests(obj, 2);
            using CancellationTokenSource source = new();
            using var stream = service.Put(source.Token);
            foreach (var req in reqs)
                stream.Send(req);
            var r = (API.Object.PutResponse)stream.Close();
            Assert.AreEqual(2, clientCache.Clients.Count);
            var o = clientCache.Clients[addrss[0][0]].Object;
            Assert.IsTrue(obj.Header.ContainerId.Equals(o.Header.ContainerId));
            Assert.IsTrue(obj.Header.OwnerId.Equals(o.Header.OwnerId));
            Assert.IsTrue(obj.Payload.Equals(o.Payload));
            Assert.IsTrue(o.CheckVerificationFields());
            o = clientCache.Clients[addrss[0][1]].Object;
            Assert.IsTrue(obj.Header.ContainerId.Equals(o.Header.ContainerId));
            Assert.IsTrue(obj.Header.OwnerId.Equals(o.Header.OwnerId));
            Assert.IsTrue(obj.Payload.Equals(o.Payload));
            Assert.IsTrue(o.CheckVerificationFields());
        }
    }
}
