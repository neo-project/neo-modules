using Grpc.Core;
using Neo.FileStorage.API.Container;
using System;
using System.Threading.Tasks;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;

namespace Neo.FileStorage.Storage.Services.Container
{
    public class ContainerServiceImpl : APIContainerService.ContainerServiceBase
    {
        public ContainerSignService SignService { get; init; }

        public override Task<AnnounceUsedSpaceResponse> AnnounceUsedSpace(AnnounceUsedSpaceRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.AnnounceUsedSpace(request, context.CancellationToken);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.Delete(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<GetResponse> Get(GetRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.Get(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<GetExtendedACLResponse> GetExtendedACL(GetExtendedACLRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.GetExtendedACL(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<ListResponse> List(ListRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.List(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<PutResponse> Put(PutRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.Put(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<SetExtendedACLResponse> SetExtendedACL(SetExtendedACLRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.SetExtendedACL(request);
                }
                catch (Exception e)
                {
                    throw new RpcException(new(StatusCode.Unknown, e.Message));
                }
            }, context.CancellationToken);
        }
    }
}
