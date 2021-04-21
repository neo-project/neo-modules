using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Reputaion.Local.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Reputaion.Local.Client
{
    public class ReputationClient
    {
        public ReputaionClientCache ClientCache { get; init; }
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
                return await FSClient.GetContainer(cid, options, context);
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
                return await FSClient.PutContainer(container, options, context);
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
                return await FSClient.ListContainers(owner, options, context);
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
                return await FSClient.GetEAclWithSignature(cid, options, context);
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
                return await FSClient.GetEACL(cid, options, context);
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
                return await FSClient.LocalNodeInfo(options, context);
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
                return await FSClient.Epoch(options, context);
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
                return await FSClient.NetworkInfo(options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObject(GetObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.GetObject(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<ObjectID> PutObject(PutObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.PutObject(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<Address> DeleteObject(DeleteObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.DeleteObject(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<FSObject> GetObjectHeader(ObjectHeaderParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.GetObjectHeader(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<byte[]> GetObjectPayloadRangeData(RangeDataParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.GetObjectPayloadRangeData(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<byte[]>> GetObjectPayloadRangeHash(RangeChecksumParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.GetObjectPayloadRangeHash(param, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }

        public async Task<List<ObjectID>> SearchObject(SearchObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            try
            {
                return await FSClient.SearchObject(param, options, context);
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
                return await FSClient.CreateSession(expiration, options, context);
            }
            catch (Exception)
            {
                SumbmitResult(false);
                throw;
            }
        }
    }
}
