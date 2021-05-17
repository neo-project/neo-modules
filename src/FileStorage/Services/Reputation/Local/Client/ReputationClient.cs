using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Reputaion.Local.Storage;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Reputaion.Local.Client
{
    public class ReputationClient
    {
        public ReputationClientCache ClientCache { get; init; }
        public FSClient FSClient { get; init; }
        public UpdatePrm Prm { get; init; }

        private void SumbmitResult(bool sat)
        {
            Prm.Sat = sat;
            Prm.Epoch = ClientCache.StorageNode.CurrentEpoch;
            ClientCache.ReputationStorage.Update(Prm);
        }

        public async Task<API.Accounting.Decimal> GetBalance(OwnerID owner = null, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var id = await FSClient.GetBalance(owner, options, context);
                SumbmitResult(true);
                return id;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSContainer> GetContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetContainer(cid, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ContainerID> PutContainer(FSContainer container, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.PutContainer(container, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task DeleteContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                await FSClient.DeleteContainer(cid, options, context);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<ContainerID>> ListContainers(OwnerID owner = null, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.ListContainers(owner, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<EAclWithSignature> GetEAclWithSignature(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetEAclWithSignature(cid, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<EACLTable> GetEACL(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetEACL(cid, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task SetEACL(EACLTable eacl, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                await FSClient.SetEACL(eacl, options, context);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task AnnounceContainerUsedSpace(List<UsedSpaceAnnouncement> announcements, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                await FSClient.AnnounceContainerUsedSpace(announcements, options, context);
                SumbmitResult(true);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<NodeInfo> LocalNodeInfo(CancellationToken context, CallOptions options = null)
        {
            try
            {
                var r = await FSClient.LocalNodeInfo(options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ulong> Epoch(CancellationToken context, CallOptions options = null)
        {
            try
            {
                var r = await FSClient.Epoch(options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<NetworkInfo> NetworkInfo(CancellationToken context, CallOptions options = null)
        {
            try
            {
                var r = await FSClient.NetworkInfo(options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObject(Address address, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetObject(address, raw, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ObjectID> PutObject(FSObject obj, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.PutObject(obj, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<Address> DeleteObject(Address address, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.DeleteObject(address, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObjectHeader(Address address, bool minimal = false, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetObjectHeader(address, minimal, raw, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<byte[]> GetObjectPayloadRangeData(Address address, FSRange range, bool raw = false, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetObjectPayloadRangeData(address, range, raw, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<byte[]>> GetObjectPayloadRangeHash(Address address, IEnumerable<FSRange> ranges, ChecksumType type, byte[] salt, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.GetObjectPayloadRangeHash(address, ranges, type, salt, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<ObjectID>> SearchObject(ContainerID cid, SearchFilters filters, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.SearchObject(cid, filters, options, context);
                SumbmitResult(true);
                return r;
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<SessionToken> CreateSession(ulong expiration, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                var r = await FSClient.CreateSession(expiration, options, context);
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
