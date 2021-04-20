using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Audit;
using Neo.FileStorage.Services.Audit.Auditor;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.Wallets;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using V2Range = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class RpcClientCache : INeoFSClientCache, IContainerCommunicator
    {
        public ClientCache clientCache;
        public Wallet wallet;

        public Client Get(string address)
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            return clientCache.Get(address); //TODO: fix no key when get client cache
        }

        public StorageGroup GetStorageGroup(AuditTask task, ObjectID id) {
            var sgAddress = new Address()
            {
                ContainerId = task.CID,
                ObjectId = id
            };
            return GetStorageGroup(task.Context,sgAddress,task.Netmap,task.ContainerNodes);
        }
        public StorageGroup GetStorageGroup(CancellationToken context, Address sgAddress, NetMap netMap, List<List<Node>> containerNodes)
        {
            List<List<Node>> nodes;
            try
            {
                nodes = NetworkMapBuilder.BuildObjectPlacement(netMap, containerNodes, sgAddress.ObjectId);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't build object placement: {0}", e.Message));
            }
            foreach (var node in nodes.Flatten())
            {
                string addr;
                try
                {
                    addr = Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
                }
                catch (Exception e)
                {
                    Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't parse remote address,address:{0},errot:{1}", node.NetworkAddress, e.Message));
                    continue;
                }
                Client cli;
                try
                {
                    cli = Get(addr);
                }
                catch (Exception e)
                {
                    Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't setup remote connection,address:{0},errot:{1}", addr, e.Message));
                    continue;
                }
                API.Object.Object obj;
                try
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    obj = cli.GetObject(new GetObjectParams { Address = sgAddress, Raw = false }, context: source.Token).Result;
                }
                catch (Exception e)
                {
                    Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't get storage group object,error:{0}", e.Message));
                    continue;
                }
                StorageGroup sg;
                try
                {
                    sg = StorageGroup.Parser.ParseFrom(obj.Payload);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("can't parse storage group payload: {0}", e.Message));
                }
                return sg;
            }
            throw new Exception("object not found");
        }

        public API.Object.Object GetHeader(AuditTask task, Node node, ObjectID id, bool relay)
        {
            bool raw = true;
            //todo
            //need set ttl
            uint ttl = 1;
            if (relay)
            {
                ttl = 10;
                raw = false;
            }

            Address objAddress = new Address()
            {
                ContainerId = task.CID,
                ObjectId = id
            };
            string addr;
            try
            {
                addr = Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't parse remote address {0}: {1}", node.NetworkAddress, e.Message));
            }
            Client client;
            try
            {
                client = Get(addr);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't setup remote connection with {0}: {1}", addr, e.Message));
            }
            API.Object.Object head;
            try
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                head = client.GetObjectHeader(new ObjectHeaderParams { Address = objAddress, Raw = raw }, context: source.Token).Result;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("object head error: {0}", e.Message));
            }
            return head;
        }

        public byte[] GetRangeHash(AuditTask task, Node node, ObjectID id, Neo.FileStorage.API.Object.Range rng)
        {
            Address objAddress = new Address()
            {
                ContainerId = task.CID,
                ObjectId = id
            };
            string addr;
            try
            {
                addr = Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't parse remote address,address:{0},error:{1}", node.NetworkAddress, e.Message));
            }
            Client cli;
            try
            {
                cli = Get(addr);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't setup remote connection with {0}:{1}", node.NetworkAddress, e.Message));
            }
            List<byte[]> result;
            try
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                result = cli.GetObjectPayloadRangeHash(new RangeChecksumParams { Address = objAddress, Ranges = new List<V2Range> { rng }, Type = ChecksumType.Tz, Salt = null }, context: source.Token).Result;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("object rangehash error :{0}", e.Message));
            }
            return result[0];
        }
    }
}
