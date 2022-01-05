using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSObject = Neo.FileStorage.API.Object.Object;
using static Neo.FileStorage.Storage.Tests.Helper;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Storage.Services.Session.Storage;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    [TestClass]
    public class UT_RemoteTarget
    {
        private class TestPutClient : IPutClient, IObjectPutClient, IRawObjectPutClient
        {
            public FSObject Object;

            public IRawObjectPutClient RawObjectPutClient()
            {
                return this;
            }

            public Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken context = default)
            {
                Object = obj;
                return Task.Run(() => obj.ObjectId);
            }

            public Task<IClientStream> PutObject(PutRequest init, DateTime? deadline = null, CancellationToken context = default)
            {
                throw new NotImplementedException();
            }
        }

        private class TestPutClientCache : IPutClientCache
        {
            public Dictionary<string, TestPutClient> Clients = new();

            public IPutClient Get(NodeInfo node)
            {
                var key = string.Join("", node.Addresses.Select(p => p.ToString()));
                Clients[key] = new TestPutClient();
                return Clients[key];
            }
        }

        [TestMethod]
        public void Test()
        {
            var source = new CancellationTokenSource();
            var ts = new TokenStore();
            var ks = new KeyStore(RandomPrivatekey().LoadPrivateKey(), ts, null);
            var prm = new PutInitPrm
            {
                CallOptions = new(),
            };
            var localAddress = "/ip4/0.0.0.0/tcp/8080";
            var clientCache = new TestPutClientCache();
            var ni = new NodeInfo();
            ni.Addresses.Add(localAddress);
            var store = new RemoteTarget
            {
                Cancellation = source.Token,
                KeyStorage = ks,
                Prm = prm,
                Node = ni,
                PutClientCache = clientCache,
            };
            var obj = RandomObject(1024);
            var body = ts.Create(new API.Session.CreateRequest
            {
                Body = new()
                {
                    OwnerId = obj.OwnerId,
                    Expiration = 10,
                }
            });
            prm.SessionToken = null;
            store.WriteHeader(obj.CutPayload());
            store.WriteChunk(obj.Payload.ToByteArray()[..(obj.Payload.Length / 2)]);
            store.WriteChunk(obj.Payload.ToByteArray()[(obj.Payload.Length / 2)..]);
            store.Close();
        }
    }
}
