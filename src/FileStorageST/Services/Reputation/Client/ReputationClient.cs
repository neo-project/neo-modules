using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Object.Get.Remote;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Storage.Services.Reputaion.Client
{
    public class ReputationClient : IFSClient, IPutClient, IGetClient
    {
        public ReputationClientCache ClientCache { get; init; }
        public IFSClient FSClient { get; init; }
        public UpdatePrm Prm { get; init; }

        private void SumbmitResult(bool sat)
        {
            Prm.Sat = sat;
            Prm.Epoch = ClientCache.EpochSource.CurrentEpoch;
            ClientCache.ReputationStorage.Update(Prm);
        }

        public void Dispose() { }

        public IFSRawClient Raw()
        {
            return FSClient.Raw();
        }

        public IRawObjectPutClient RawObjectPutClient()
        {
            return FSClient.Raw();
        }

        public IRawObjectGetClient RawObjectGetClient()
        {
            return FSClient.Raw();
        }

        public async Task<API.Accounting.Decimal> GetBalance(OwnerID owner = null, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var id = await FSClient.GetBalance(owner, options, cancellation);
                SumbmitResult(true);
                return id;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ContainerWithSignature> GetContainer(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetContainer(cid, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ContainerID> PutContainer(FSContainer container, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.PutContainer(container, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task DeleteContainer(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                await FSClient.DeleteContainer(cid, options, cancellation);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<ContainerID>> ListContainers(OwnerID owner = null, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.ListContainers(owner, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<EAclWithSignature> GetEAcl(ContainerID cid, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetEAcl(cid, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task SetEACL(EACLTable eacl, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                await FSClient.SetEACL(eacl, options, cancellation);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task AnnounceTrust(ulong epoch, List<API.Reputation.Trust> trusts, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                await FSClient.AnnounceTrust(epoch, trusts, options, cancellation);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task AnnounceIntermediateTrust(ulong epoch, uint iter, PeerToPeerTrust trust, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                await FSClient.AnnounceIntermediateTrust(epoch, iter, trust, options, cancellation);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task AnnounceContainerUsedSpace(List<UsedSpaceAnnouncement> announcements, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                await FSClient.AnnounceContainerUsedSpace(announcements, options, cancellation);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<NodeInfo> LocalNodeInfo(CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.LocalNodeInfo(options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ulong> Epoch(CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.Epoch(options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<NetworkInfo> NetworkInfo(CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.NetworkInfo(options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObject(Address address, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetObject(address, raw, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.PutObject(obj, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<Address> DeleteObject(Address address, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.DeleteObject(address, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObjectHeader(Address address, bool minimal = false, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetObjectHeader(address, minimal, raw, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<byte[]> GetObjectPayloadRangeData(Address address, FSRange range, bool raw = false, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetObjectPayloadRangeData(address, range, raw, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<FSRange> ranges, ChecksumType type, byte[] salt, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.GetObjectPayloadRangeHash(address, ranges, type, salt, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.SearchObject(cid, filters, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<SessionToken> CreateSession(ulong expiration, CallOptions options = null, CancellationToken cancellation = default)
        {
            try
            {
                var r = await FSClient.CreateSession(expiration, options, cancellation);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }
    }
}
