using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Reputaion.Storage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FSClient = Neo.FileStorage.API.Client.Client;
using FSContainer = Neo.FileStorage.API.Container.Container;
using FSObject = Neo.FileStorage.API.Object.Object;
using UsedSpaceAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Reputaion
{
    public class ReputationClient
    {
        public FSClient FSClient { get; init; }
        public UpdatePrm Prm { get; init; }

        public async Task<API.Accounting.Decimal> GetBalance(OwnerID owner = null, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetBalance(owner, options);
        }

        public async Task<FSContainer> GetContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetContainer(cid, options, context);
        }

        public async Task<ContainerID> PutContainer(FSContainer container, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.PutContainer(container, options, context);
        }

        public async Task DeleteContainer(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            await FSClient.DeleteContainer(cid, options, context);
        }

        public async Task<List<ContainerID>> ListContainers(OwnerID owner = null, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.ListContainers(owner, options, context);
        }

        public async Task<EAclWithSignature> GetEAclWithSignature(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetEAclWithSignature(cid, options, context);
        }

        public async Task<EACLTable> GetEACL(ContainerID cid, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetEACL(cid, options, context);
        }

        public async Task SetEACL(EACLTable eacl, CallOptions options = null, CancellationToken context = default)
        {
            await FSClient.SetEACL(eacl, options, context);
        }

        public async Task AnnounceContainerUsedSpace(List<UsedSpaceAnnouncement> announcements, CallOptions options = null, CancellationToken context = default)
        {
            await FSClient.AnnounceContainerUsedSpace(announcements, options, context);
        }

        public async Task<NodeInfo> LocalNodeInfo(CancellationToken context, CallOptions options = null)
        {
            return await FSClient.LocalNodeInfo(options, context);
        }

        public async Task<ulong> Epoch(CancellationToken context, CallOptions options = null)
        {
            return await FSClient.Epoch(options, context);
        }

        public async Task<NetworkInfo> NetworkInfo(CancellationToken context, CallOptions options = null)
        {
            return await FSClient.NetworkInfo(options, context);
        }

        public async Task<FSObject> GetObject(GetObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetObject(param, options, context);
        }

        public async Task<ObjectID> PutObject(PutObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.PutObject(param, options, context);
        }

        public async Task<Address> DeleteObject(DeleteObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.DeleteObject(param, options, context);
        }

        public async Task<FSObject> GetObjectHeader(ObjectHeaderParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetObjectHeader(param, options, context);
        }

        public async Task<byte[]> GetObjectPayloadRangeData(RangeDataParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetObjectPayloadRangeData(param, options, context);
        }

        public async Task<List<byte[]>> GetObjectPayloadRangeHash(RangeChecksumParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.GetObjectPayloadRangeHash(param, options, context);
        }

        public async Task<List<ObjectID>> SearchObject(SearchObjectParams param, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.SearchObject(param, options, context);
        }

        public async Task<SessionToken> CreateSession(ulong expiration, CallOptions options = null, CancellationToken context = default)
        {
            return await FSClient.CreateSession(expiration, options, context);
        }
    }
}
