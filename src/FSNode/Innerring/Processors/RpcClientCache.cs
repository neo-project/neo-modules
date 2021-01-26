using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Audit;
using Neo.FSNode.Services.Audit.Auditor;
using Neo.FSNode.Services.ObjectManager.Placement;
using Neo.Wallets;
using NeoFS.API.v2.Client;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.StorageGroup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using V2Range = NeoFS.API.v2.Object.Range;

namespace Neo.Plugins.Innerring.Processors
{
    public class RpcClientCache : INeoFSClientCache, IContainerCommunicator
    {
        public ClientCache clientCache;
        public Wallet wallet;

        public Client Get(string address, params Option[] opts)
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            return clientCache.GetClient(accounts.ToArray()[0].GetKey().PrivateKey, address);
        }

        public StorageGroup GetStorageGroup(AuditTask task, ObjectID id)
        {
            List<List<Node>> nodes;
            try
            {
                nodes = NetworkMapBuilder.BuildObjectPlacement(task.Netmap, task.ContainerNodes, id);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("can't build object placement: {0}", e.Message));
            }
            var sgAddress = new Address()
            {
                ContainerId = task.CID,
                ObjectId = id
            };
            foreach (var node in nodes.Flatten())
            {
                string addr;
                try
                {
                    addr = FSNode.Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't parse remote address,address:{0},errot:{1}", node.NetworkAddress, e.Message));
                    continue;
                }
                Client cli;
                try
                {
                    cli = Get(addr);
                }
                catch (Exception e)
                {
                    Neo.Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't setup remote connection,address:{0},errot:{1}", addr, e.Message));
                    continue;
                }
                NeoFS.API.v2.Object.Object obj;
                try
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    obj = cli.GetObject(source.Token, new GetObjectParams { Address = sgAddress, Raw = false }).Result;
                }
                catch (Exception e)
                {
                    Neo.Utility.Log("RpcClientCache", LogLevel.Warning, string.Format("can't get storage group object,error:{0}", e.Message));
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

        public NeoFS.API.v2.Object.Object GetHeader(AuditTask task, Node node, ObjectID id, bool relay)
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
                addr = FSNode.Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
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
            NeoFS.API.v2.Object.Object head;
            try
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                head = client.GetObjectHeader(source.Token, new ObjectHeaderParams { Address = objAddress, Raw = raw });
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("object head error: {0}", e.Message));
            }
            return head;
        }

        public byte[] GetRangeHash(AuditTask task, Node node, ObjectID id, NeoFS.API.v2.Object.Range rng)
        {
            Address objAddress = new Address()
            {
                ContainerId = task.CID,
                ObjectId = id
            };
            string addr;
            try
            {
                addr = FSNode.Network.Address.IPAddrFromMultiaddr(node.NetworkAddress);
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
                result = cli.GetObjectPayloadRangeHash(source.Token, new RangeChecksumParams { Address = objAddress, Ranges = new List<V2Range> { rng }, Type = ChecksumType.Tz, Salt = null });
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("object rangehash error :{0}", e.Message));
            }
            return result[0];
        }
    }
}
