using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.Cache;
using Neo.FileStorage.InnerRing.Services.Audit;
using Neo.FileStorage.InnerRing.Services.Audit.Auditor;
using Neo.FileStorage.Placement;
using Neo.Wallets;

namespace Neo.FileStorage.InnerRing
{
    public class RpcClientCache : IFSClientCache, IContainerCommunicator
    {
        public ClientCache ClientCache = new();
        public Wallet Wallet;

        public void Dispose()
        {
            ClientCache.Dispose();
        }

        public IFSClient Get(NodeInfo node)
        {
            return ClientCache.Get(node);
        }

        public StorageGroup GetStorageGroup(AuditTask task, ObjectID id)
        {
            var sgAddress = new Address()
            {
                ContainerId = task.ContainerID,
                ObjectId = id
            };
            return GetStorageGroup(task.Cancellation, sgAddress, task.Netmap, task.ContainerNodes);
        }

        public StorageGroup GetStorageGroup(CancellationToken cancellation, Address sgAddress, NetMap netMap, List<List<Node>> containerNodes)
        {
            List<List<Node>> nodes = NetworkMapBuilder.BuildObjectPlacement(netMap, containerNodes, sgAddress.ObjectId);
            foreach (var node in nodes.Flatten())
            {
                IFSClient cli;
                try
                {
                    cli = Get(node.Info);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(RpcClientCache), LogLevel.Warning, $"can't setup remote connection, error={e.Message}");
                    continue;
                }
                API.Object.Object obj;
                try
                {
                    using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    var key = Wallet.GetAccounts().ToArray()[0].GetKey().Export().LoadWif();
                    obj = cli.GetObject(sgAddress, false, new CallOptions() { Key = key }, context: source.Token).Result;
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(RpcClientCache), LogLevel.Warning, $"can't get storage group object, error={e.Message}");
                    continue;
                }
                StorageGroup sg = StorageGroup.Parser.ParseFrom(obj.Payload);
                return sg;
            }
            throw new Exception("object not found");
        }

        public API.Object.Object GetHeader(AuditTask task, Node node, ObjectID id, bool relay)
        {
            bool raw = !relay;
            uint ttl = relay ? 10u : 1u;
            Address objAddress = new()
            {
                ContainerId = task.ContainerID,
                ObjectId = id
            };
            IFSClient client = Get(node.Info);
            using var source = CancellationTokenSource.CreateLinkedTokenSource(task.Cancellation);
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var key = Wallet.GetAccounts().ToArray()[0].GetKey().Export().LoadWif();
            return client.GetObjectHeader(objAddress, raw, options: new CallOptions() { Key = key, Ttl = ttl }, context: source.Token).Result;
        }

        public byte[] GetRangeHash(AuditTask task, Node node, ObjectID id, API.Object.Range rng)
        {
            Address objAddress = new()
            {
                ContainerId = task.ContainerID,
                ObjectId = id
            };
            IFSClient cli = Get(node.Info);
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var key = Wallet.GetAccounts().ToArray()[0].GetKey().PrivateKey.LoadPrivateKey();
            List<byte[]> result = cli.GetObjectPayloadRangeHash(objAddress, new List<API.Object.Range> { rng }, ChecksumType.Tz, null, new() { Ttl = 1, Key = key }, source.Token).Result;
            return result[0];
        }
    }
}
