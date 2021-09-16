using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Client;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;
using System.Collections.Concurrent;

namespace Neo.FileStorage.Cache
{
    public class MultiClient : IFSClient, IFSRawClient
    {
        private readonly List<Network.Address> addresses;
        private readonly ConcurrentDictionary<Network.Address, Client> clients = new();

        public MultiClient(List<Network.Address> addrs)
        {
            addresses = addrs;
        }

        public void Dispose()
        {
            lock (clients)
            {
                addresses.Clear();
                foreach (var cli in clients.Values)
                    cli.Dispose();
                clients.Clear();
            }
        }

        public IFSRawClient Raw()
        {
            return this;
        }

        private Client Client(Network.Address address)
        {
            if (!clients.TryGetValue(address, out var client))
            {
                client = new(null, "http://" + address.ToHostAddressString());
                clients[address] = client;
            }
            return client;
        }

        private void IterateClients(Action<Client> handler, CancellationToken token)
        {
            Exception lastErr = null;
            foreach (var address in addresses)
            {
                if (token.IsCancellationRequested) throw new TaskCanceledException();
                try
                {
                    var client = Client(address);
                    handler(client);
                    return;
                }
                catch (Exception e)
                {
                    if (e is AggregateException ae)
                    {
                        foreach (var ie in ae.InnerExceptions)
                        {
                            if (ie is Grpc.Core.RpcException re && re.StatusCode == Grpc.Core.StatusCode.Cancelled)
                                throw;
                            lastErr = ie;
                        }
                    }
                    else
                        lastErr = e;
                    continue;
                }
            }
            if (lastErr is not null)
                throw lastErr;
            throw new InvalidOperationException($"handle request failed");
        }

        public Task<API.Accounting.Decimal> GetBalance(OwnerID owner, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Accounting.Decimal balance = null;
                IterateClients(client =>
                {
                    balance = client.GetBalance(owner, options, context).Result;
                }, context);
                return balance;
            }, context);
        }

        public Task<API.Accounting.Decimal> GetBalance(BalanceRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Accounting.Decimal balance = null;
                IterateClients(client =>
                {
                    balance = client.GetBalance(request, deadline, context).Result;
                }, context);
                return balance;
            }, context);
        }

        public Task<ContainerWithSignature> GetContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ContainerWithSignature container = null;
                IterateClients(client =>
                {
                    container = client.GetContainer(cid, options, context).Result;
                }, context);
                return container;
            }, context);
        }

        public Task<ContainerID> PutContainer(Container container, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ContainerID cid = null;
                IterateClients(client =>
                {
                    cid = client.PutContainer(container, options, context).Result;
                }, context);
                return cid;
            }, context);
        }

        public Task DeleteContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.DeleteContainer(cid, options, context).Wait();
                }, context);
            }, context);
        }

        public Task<List<ContainerID>> ListContainers(OwnerID owner, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<ContainerID> cids = null;
                IterateClients(client =>
                {
                    cids = client.ListContainers(owner, options, context).Result;
                }, context);
                return cids;
            }, context);
        }

        public Task<EAclWithSignature> GetEAcl(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                EAclWithSignature eacl = null;
                IterateClients(client =>
                {
                    eacl = client.GetEAcl(cid, options, context).Result;
                }, context);
                return eacl;
            }, context);
        }

        public Task SetEACL(EACLTable eacl, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.SetEACL(eacl, options, context).Wait();
                }, context);
            }, context);
        }

        public Task AnnounceContainerUsedSpace(List<UsedSpaceAnnouncement> announcements, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceContainerUsedSpace(announcements, options, context).Wait();
                }, context);
            }, context);
        }

        public Task<ContainerWithSignature> GetContainer(API.Container.GetRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ContainerWithSignature container = null;
                IterateClients(client =>
                {
                    container = client.GetContainer(request, deadline, context).Result;
                }, context);
                return container;
            }, context);
        }

        public Task<ContainerID> PutContainer(API.Container.PutRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ContainerID cid = null;
                IterateClients(client =>
                {
                    cid = client.PutContainer(request, deadline, context).Result;
                }, context);
                return cid;
            }, context);
        }

        public Task DeleteContainer(API.Container.DeleteRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.DeleteContainer(request, deadline, context).Wait();
                }, context);
            }, context);
        }

        public Task<List<ContainerID>> ListContainers(ListRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<ContainerID> cids = null;
                IterateClients(client =>
                {
                    cids = client.ListContainers(request, deadline, context).Result;
                }, context);
                return cids;
            }, context);
        }

        public Task<EAclWithSignature> GetEAcl(GetExtendedACLRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                EAclWithSignature eacl = null;
                IterateClients(client =>
                {
                    eacl = client.GetEAcl(request, deadline, context).Result;
                }, context);
                return eacl;
            }, context);
        }

        public Task SetEACL(SetExtendedACLRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.SetEACL(request, deadline, context).Wait();
                }, context);
            }, context);
        }

        public Task AnnounceContainerUsedSpace(AnnounceUsedSpaceRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceContainerUsedSpace(request, deadline, context).Wait();
                }, context);
            }, context);
        }


        public Task<NodeInfo> LocalNodeInfo(CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                NodeInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.LocalNodeInfo(options, context).Result;
                }, context);
                return ni;
            }, context);
        }

        public Task<ulong> Epoch(CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ulong epoch = 0;
                IterateClients(client =>
                {
                    epoch = client.Epoch(options, context).Result;
                }, context);
                return epoch;
            }, context);
        }

        public Task<NetworkInfo> NetworkInfo(CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                NetworkInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.NetworkInfo(options, context).Result;
                }, context);
                return ni;
            }, context);
        }

        public Task<NodeInfo> LocalNodeInfo(LocalNodeInfoRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                NodeInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.LocalNodeInfo(request, deadline, context).Result;
                }, context);
                return ni;
            }, context);
        }

        public Task<ulong> Epoch(LocalNodeInfoRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ulong epoch = 0;
                IterateClients(client =>
                {
                    epoch = client.Epoch(request, deadline, context).Result;
                }, context);
                return epoch;
            }, context);
        }


        public Task<API.Object.Object> GetObject(Address address, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object obj = null;
                IterateClients(client =>
                {
                    obj = client.GetObject(address, raw, options, context).Result;
                }, context);
                return obj;
            }, context);
        }

        public Task<ObjectID> PutObject(API.Object.Object obj, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                ObjectID oid = null;
                IterateClients(client =>
                {
                    oid = client.PutObject(obj, options, context).Result;
                }, context);
                return oid;
            }, context);
        }

        public Task<Address> DeleteObject(Address address, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                Address address = null;
                IterateClients(client =>
                {
                    address = client.DeleteObject(address, options, context).Result;
                }, context);
                return address;
            }, context);
        }

        public Task<API.Object.Object> GetObjectHeader(Address address, bool minimal = false, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object header = null;
                IterateClients(client =>
                {
                    header = client.GetObjectHeader(address, minimal, raw, options, context).Result;
                }, context);
                return header;
            }, context);
        }

        public Task<byte[]> GetObjectPayloadRangeData(Address address, API.Object.Range range, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                byte[] data = null;
                IterateClients(client =>
                {
                    data = client.GetObjectPayloadRangeData(address, range, raw, options, context).Result;
                }, context);
                return data;
            }, context);
        }

        public Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<API.Object.Range> ranges, ChecksumType type, byte[] salt, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<byte[]> hashes = null;
                IterateClients(client =>
                {
                    hashes = client.GetObjectPayloadRangeHash(address, ranges, type, salt, options, context).Result;
                }, context);
                return hashes;
            }, context);
        }

        public Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<ObjectID> oids = null;
                IterateClients(client =>
                {
                    oids = client.SearchObject(cid, filters, options, context).Result;
                }, context);
                return oids;
            }, context);
        }

        public Task<API.Object.Object> GetObject(API.Object.GetRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object obj = null;
                IterateClients(client =>
                {
                    obj = client.GetObject(request, deadline, context).Result;
                }, context);
                return obj;
            }, context);
        }

        public Task<IClientStream> PutObject(API.Object.PutRequest init, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IClientStream putStream = null;
                IterateClients(client =>
                {
                    putStream = client.PutObject(init, deadline, context).Result;
                }, context);
                return putStream;
            }, context);
        }

        public Task<Address> DeleteObject(API.Object.DeleteRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                Address address = null;
                IterateClients(client =>
                {
                    address = client.DeleteObject(request, deadline, context).Result;
                }, context);
                return address;
            }, context);
        }

        public Task<API.Object.Object> GetObjectHeader(HeadRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object header = null;
                IterateClients(client =>
                {
                    header = client.GetObjectHeader(request, deadline, context).Result;
                }, context);
                return header;
            }, context);
        }

        public Task<byte[]> GetObjectPayloadRangeData(GetRangeRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                byte[] data = null;
                IterateClients(client =>
                {
                    data = client.GetObjectPayloadRangeData(request, deadline, context).Result;
                }, context);
                return data;
            }, context);
        }

        public Task<List<byte[]>> GetObjectPayloadRangeHash(GetRangeHashRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<byte[]> hashes = null;
                IterateClients(client =>
                {
                    hashes = client.GetObjectPayloadRangeHash(request, deadline, context).Result;
                }, context);
                return hashes;
            }, context);
        }

        public Task<List<ObjectID>> SearchObject(SearchRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                List<ObjectID> oids = null;
                IterateClients(client =>
                {
                    oids = client.SearchObject(request, deadline, context).Result;
                }, context);
                return oids;
            }, context);
        }

        public Task AnnounceTrust(ulong epoch, List<Trust> trusts, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceTrust(epoch, trusts, options, context).Wait();
                }, context);
            }, context);
        }

        public Task AnnounceIntermediateTrust(ulong epoch, uint iter, PeerToPeerTrust trust, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceIntermediateTrust(epoch, iter, trust, options, context).Wait();
                }, context);
            }, context);
        }

        public Task AnnounceTrust(AnnounceLocalTrustRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceTrust(request, deadline, context).Wait();
                }, context);
            }, context);
        }

        public Task AnnounceIntermediateTrust(AnnounceIntermediateResultRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceIntermediateTrust(request, deadline, context).Wait();
                }, context);
            }, context);
        }

        public Task<SessionToken> CreateSession(ulong expiration, CallOptions options = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                SessionToken session = null;
                IterateClients(client =>
                {
                    session = client.CreateSession(expiration, options, context).Result;
                }, context);
                return session;
            }, context);
        }

        public Task<SessionToken> CreateSession(CreateRequest request, DateTime? deadline = null, CancellationToken context = default)
        {
            return Task.Run(() =>
            {
                SessionToken session = null;
                IterateClients(client =>
                {
                    session = client.CreateSession(request, deadline, context).Result;
                }, context);
                return session;
            }, context);
        }
    }
}
