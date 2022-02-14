using Google.Protobuf;
using Neo.FileStorage.API.Accounting;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Client;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Neo.FileStorage.Network.Helper;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Cache
{
    public class MultiClient : IFSClient, IFSRawClient
    {
        public const string ErrWrongPublicKey = "public key is different from the key in the network map";
        private readonly List<Network.Address> addresses;
        private readonly ByteString publicKey;
        private readonly ConcurrentDictionary<Network.Address, Client> clients = new();

        public MultiClient(NodeInfo ni)
        {
            addresses = ni.Addresses.ToList().ToNetworkAddresses();
            publicKey = ni.PublicKey;
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

        private void AssertKeyResponseCallback(IResponse resp)
        {
            if (!resp.VerifyHeader.BodySignature.Key.Equals(publicKey))
                throw new InvalidOperationException(ErrWrongPublicKey);
        }

        private Client Client(Network.Address address)
        {
            if (!clients.TryGetValue(address, out var client))
            {
                client = new Client(null, address.ToRpcAddressString()).WithResponseInfoHandler(AssertKeyResponseCallback);
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

        public Task<API.Accounting.Decimal> GetBalance(OwnerID owner, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Accounting.Decimal balance = null;
                IterateClients(client =>
                {
                    balance = client.GetBalance(owner, options, cancellation).Result;
                }, cancellation);
                return balance;
            }, cancellation);

        }

        public Task<API.Accounting.Decimal> GetBalance(BalanceRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Accounting.Decimal balance = null;
                IterateClients(client =>
                {
                    balance = client.GetBalance(request, deadline, cancellation).Result;
                }, cancellation);
                return balance;
            }, cancellation);
        }

        public Task<ContainerWithSignature> GetContainer(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ContainerWithSignature container = null;
                IterateClients(client =>
                {
                    container = client.GetContainer(cid, options, cancellation).Result;
                }, cancellation);
                return container;
            }, cancellation);
        }

        public Task<ContainerID> PutContainer(Container container, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ContainerID cid = null;
                IterateClients(client =>
                {
                    cid = client.PutContainer(container, options, cancellation).Result;
                }, cancellation);
                return cid;
            }, cancellation);
        }

        public Task DeleteContainer(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.DeleteContainer(cid, options, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task<List<ContainerID>> ListContainers(OwnerID owner, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<ContainerID> cids = null;
                IterateClients(client =>
                {
                    cids = client.ListContainers(owner, options, cancellation).Result;
                }, cancellation);
                return cids;
            }, cancellation);
        }

        public Task<EAclWithSignature> GetEAcl(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                EAclWithSignature eacl = null;
                IterateClients(client =>
                {
                    eacl = client.GetEAcl(cid, options, cancellation).Result;
                }, cancellation);
                return eacl;
            }, cancellation);
        }

        public Task SetEACL(EACLTable eacl, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.SetEACL(eacl, options, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task AnnounceContainerUsedSpace(List<UsedSpaceAnnouncement> announcements, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceContainerUsedSpace(announcements, options, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task<ContainerWithSignature> GetContainer(API.Container.GetRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ContainerWithSignature container = null;
                IterateClients(client =>
                {
                    container = client.GetContainer(request, deadline, cancellation).Result;
                }, cancellation);
                return container;
            }, cancellation);
        }

        public Task<ContainerID> PutContainer(API.Container.PutRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ContainerID cid = null;
                IterateClients(client =>
                {
                    cid = client.PutContainer(request, deadline, cancellation).Result;
                }, cancellation);
                return cid;
            }, cancellation);
        }

        public Task DeleteContainer(API.Container.DeleteRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.DeleteContainer(request, deadline, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task<List<ContainerID>> ListContainers(ListRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<ContainerID> cids = null;
                IterateClients(client =>
                {
                    cids = client.ListContainers(request, deadline, cancellation).Result;
                }, cancellation);
                return cids;
            }, cancellation);
        }

        public Task<EAclWithSignature> GetEAcl(GetExtendedACLRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                EAclWithSignature eacl = null;
                IterateClients(client =>
                {
                    eacl = client.GetEAcl(request, deadline, cancellation).Result;
                }, cancellation);
                return eacl;
            }, cancellation);
        }

        public Task SetEACL(SetExtendedACLRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.SetEACL(request, deadline, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task AnnounceContainerUsedSpace(AnnounceUsedSpaceRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceContainerUsedSpace(request, deadline, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }


        public Task<NodeInfo> LocalNodeInfo(CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                NodeInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.LocalNodeInfo(options, cancellation).Result;
                }, cancellation);
                return ni;
            }, cancellation);
        }

        public Task<ulong> Epoch(CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ulong epoch = 0;
                IterateClients(client =>
                {
                    epoch = client.Epoch(options, cancellation).Result;
                }, cancellation);
                return epoch;
            }, cancellation);
        }

        public Task<NetworkInfo> NetworkInfo(CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                NetworkInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.NetworkInfo(options, cancellation).Result;
                }, cancellation);
                return ni;
            }, cancellation);
        }

        public Task<NodeInfo> LocalNodeInfo(LocalNodeInfoRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                NodeInfo ni = null;
                IterateClients(client =>
                {
                    ni = client.LocalNodeInfo(request, deadline, cancellation).Result;
                }, cancellation);
                return ni;
            }, cancellation);
        }

        public Task<ulong> Epoch(LocalNodeInfoRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ulong epoch = 0;
                IterateClients(client =>
                {
                    epoch = client.Epoch(request, deadline, cancellation).Result;
                }, cancellation);
                return epoch;
            }, cancellation);
        }


        public Task<API.Object.Object> GetObject(Address address, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object obj = null;
                IterateClients(client =>
                {
                    obj = client.GetObject(address, raw, options, cancellation).Result;
                }, cancellation);
                return obj;
            }, cancellation);
        }

        public Task<ObjectID> PutObject(API.Object.Object obj, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                ObjectID oid = null;
                IterateClients(client =>
                {
                    oid = client.PutObject(obj, options, cancellation).Result;
                }, cancellation);
                return oid;
            }, cancellation);
        }

        public Task<Address> DeleteObject(Address address, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                Address address = null;
                IterateClients(client =>
                {
                    address = client.DeleteObject(address, options, cancellation).Result;
                }, cancellation);
                return address;
            }, cancellation);
        }

        public Task<API.Object.Object> GetObjectHeader(Address address, bool minimal = false, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object header = null;
                IterateClients(client =>
                {
                    header = client.GetObjectHeader(address, minimal, raw, options, cancellation).Result;
                }, cancellation);
                return header;
            }, cancellation);
        }

        public Task<byte[]> GetObjectPayloadRangeData(Address address, API.Object.Range range, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                byte[] data = null;
                IterateClients(client =>
                {
                    data = client.GetObjectPayloadRangeData(address, range, raw, options, cancellation).Result;
                }, cancellation);
                return data;
            }, cancellation);
        }

        public Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<API.Object.Range> ranges, ChecksumType type, byte[] salt, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<byte[]> hashes = null;
                IterateClients(client =>
                {
                    hashes = client.GetObjectPayloadRangeHash(address, ranges, type, salt, options, cancellation).Result;
                }, cancellation);
                return hashes;
            }, cancellation);
        }

        public Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<ObjectID> oids = null;
                IterateClients(client =>
                {
                    oids = client.SearchObject(cid, filters, options, cancellation).Result;
                }, cancellation);
                return oids;
            }, cancellation);
        }

        public Task<API.Object.Object> GetObject(API.Object.GetRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object obj = null;
                IterateClients(client =>
                {
                    obj = client.GetObject(request, deadline, cancellation).Result;
                }, cancellation);
                return obj;
            }, cancellation);
        }

        public Task<IClientStream> PutObject(API.Object.PutRequest init, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IClientStream putStream = null;
                IterateClients(client =>
                {
                    putStream = client.PutObject(init, deadline, cancellation).Result;
                }, cancellation);
                return putStream;
            }, cancellation);
        }

        public Task<Address> DeleteObject(API.Object.DeleteRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                Address address = null;
                IterateClients(client =>
                {
                    address = client.DeleteObject(request, deadline, cancellation).Result;
                }, cancellation);
                return address;
            }, cancellation);
        }

        public Task<API.Object.Object> GetObjectHeader(HeadRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                API.Object.Object header = null;
                IterateClients(client =>
                {
                    header = client.GetObjectHeader(request, deadline, cancellation).Result;
                }, cancellation);
                return header;
            }, cancellation);
        }

        public Task<byte[]> GetObjectPayloadRangeData(GetRangeRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                byte[] data = null;
                IterateClients(client =>
                {
                    data = client.GetObjectPayloadRangeData(request, deadline, cancellation).Result;
                }, cancellation);
                return data;
            }, cancellation);
        }

        public Task<List<byte[]>> GetObjectPayloadRangeHash(GetRangeHashRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<byte[]> hashes = null;
                IterateClients(client =>
                {
                    hashes = client.GetObjectPayloadRangeHash(request, deadline, cancellation).Result;
                }, cancellation);
                return hashes;
            }, cancellation);
        }

        public Task<List<ObjectID>> SearchObject(SearchRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                List<ObjectID> oids = null;
                IterateClients(client =>
                {
                    oids = client.SearchObject(request, deadline, cancellation).Result;
                }, cancellation);
                return oids;
            }, cancellation);
        }


        public Task AnnounceTrust(ulong epoch, List<Trust> trusts, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceTrust(epoch, trusts, options, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task AnnounceIntermediateTrust(ulong epoch, uint iter, PeerToPeerTrust trust, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceIntermediateTrust(epoch, iter, trust, options, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task AnnounceTrust(AnnounceLocalTrustRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceTrust(request, deadline, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task AnnounceIntermediateTrust(AnnounceIntermediateResultRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                IterateClients(client =>
                {
                    client.AnnounceIntermediateTrust(request, deadline, cancellation).Wait();
                }, cancellation);
            }, cancellation);
        }

        public Task<SessionToken> CreateSession(ulong expiration, CallOptions options = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                SessionToken session = null;
                IterateClients(client =>
                {
                    session = client.CreateSession(expiration, options, cancellation).Result;
                }, cancellation);
                return session;
            }, cancellation);
        }

        public Task<SessionToken> CreateSession(CreateRequest request, DateTime? deadline = null, CancellationToken cancellation = default)
        {
            return Task.Run(() =>
            {
                SessionToken session = null;
                IterateClients(client =>
                {
                    session = client.CreateSession(request, deadline, cancellation).Result;
                }, cancellation);
                return session;
            }, cancellation);
        }
    }
}
