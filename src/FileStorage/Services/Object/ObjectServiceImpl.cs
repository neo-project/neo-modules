using Grpc.Core;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Services.Object.Acl;
using Neo.FileStorage.Services.Object.Delete;
using Neo.FileStorage.Services.Object.Get;
using Neo.FileStorage.Services.Object.Put;
using Neo.FileStorage.Services.Object.Search;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object
{
    public partial class ObjectServiceImpl : ObjectService.ObjectServiceBase
    {
        private readonly ECDsa key;
        private readonly StorageEngine localStorage;
        private readonly IContainerSource contnainerSource;
        private readonly INetmapSource netmapSource;
        private readonly INetState netmapState;
        private readonly DeleteService deleteService;
        private readonly GetService getService;
        private readonly PutService putService;
        private readonly SearchService searchService;
        private readonly AclChecker aclChecker;
        private readonly Responser responser;
        private readonly IClient morph;

        public ObjectServiceImpl(IContainerSource container_source, StorageEngine local_storage, IClient client, INetState state)
        {
            localStorage = local_storage;
            morph = client;
            contnainerSource = container_source;
            netmapState = state;
            deleteService = new DeleteService();
            getService = new GetService();
            putService = new PutService();
            searchService = new SearchService();
            aclChecker = new ()
            {
                Morph = client,
                LocalStorage = local_storage,
                EAclValidator = new ()
                {
                    EAclStorage = new (morph),
                },
                NetmapState = state,
            };
            responser = new ()
            {
                Key = key,
            };
        }

        public override Task Get(GetRequest request, IServerStreamWriter<GetResponse> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                RequestInfo info;
                try
                {
                    info = aclChecker.CheckRequest(request, Operation.Get);
                }
                catch (Exception e)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
                }
                if (!request.VerifyRequest()) throw new RpcException(new Status(StatusCode.Unauthenticated, "verify header failed"));
                var prm = GetPrm.FromRequest(request);
                GetWriter writer = new ()
                {
                    Stream = responseStream,
                    Responser = new () { Key = key },
                    AclChecker = aclChecker,
                    Info = info
                };
                prm.HeaderWriter = writer;
                prm.ChunkWriter = writer;
                getService.Get(prm);
            }, context.CancellationToken);
        }

        public override async Task<PutResponse> Put(IAsyncStreamReader<PutRequest> requestStream, ServerCallContext context)
        {
            var init_received = false;
            IObjectTarget target = null;
            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                if (!init_received)
                {
                    if (request.Body.ObjectPartCase != PutRequest.Types.Body.ObjectPartOneofCase.Init)
                        throw new RpcException(new Status(StatusCode.DataLoss, " missing init"));
                    var init = request.Body.Init;
                    if (!init.VerifyRequest()) throw new RpcException(new Status(StatusCode.Unauthenticated, " verify header failed"));
                    var put_init_prm = PutInitPrm.FromRequest(request);
                    try
                    {
                        target = putService.Init(put_init_prm);
                    }
                    catch (Exception e)
                    {
                        throw new RpcException(new Status(StatusCode.FailedPrecondition, e.Message));
                    }

                }
                else
                {
                    if (request.Body.ObjectPartCase != PutRequest.Types.Body.ObjectPartOneofCase.Chunk)
                        throw new RpcException(new Status(StatusCode.DataLoss, " missing chunk"));
                    if (target is null) throw new RpcException(new Status(StatusCode.DataLoss, "init missing"));
                    target.WriteChunk(request.Body.Chunk.ToByteArray());
                }
            }
            try
            {
                var result = target.Close();
                var resp = new PutResponse
                {
                    Body = new PutResponse.Types.Body
                    {
                        ObjectId = result.Self,
                    }
                };
                return resp;
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message));
            }
        }

        public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
        {
            try
            {
                aclChecker.CheckRequest(request, Operation.Delete);
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var prm = DeletePrm.FromRequest(request);
                var address = deleteService.Delete(prm);
                var resp = new DeleteResponse
                {
                    Body = new DeleteResponse.Types.Body
                    {
                        Tombstone = address,
                    }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<HeadResponse> Head(HeadRequest request, ServerCallContext context)
        {
            RequestInfo info;
            try
            {
                info = aclChecker.CheckRequest(request, Operation.Head);
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            if (!request.VerifyRequest()) throw new RpcException(new Status(StatusCode.Unauthenticated, " verify failed"));
            return Task.Run(() =>
            {
                var head_prm = HeadPrm.FromRequest(request);
                var header = getService.Head(head_prm);
                return responser.HeadResponse(head_prm.Short, header);
            }, context.CancellationToken);
        }


        public override Task GetRange(GetRangeRequest request, IServerStreamWriter<GetRangeResponse> responseStream, ServerCallContext context)
        {
            try
            {
                aclChecker.CheckRequest(request, Operation.Getrange);
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var head_prm = RangePrm.FromRequest(request);
                var writer = new RangeWriter(responseStream, responser);
                head_prm.ChunkWriter = writer;
                getService.GetRange(head_prm);
            }, context.CancellationToken);
        }

        public override Task<GetRangeHashResponse> GetRangeHash(GetRangeHashRequest request, ServerCallContext context)
        {
            try
            {
                aclChecker.CheckRequest(request, Operation.Getrangehash);
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var prm = RangeHashPrm.FromRequest(request);
                return responser.GetRangeHashResponse(getService.GetRangeHash(prm));
            }, context.CancellationToken);
        }

        public override Task Search(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            try
            {
                aclChecker.CheckRequest(request, Operation.Search);
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var prm = SearchPrm.FromRequest(request);
                var oids = searchService.Search(prm);
                var resp = new SearchResponse
                {
                    Body = new SearchResponse.Types.Body { }
                };
                resp.Body.IdList.AddRange(oids);
                key.SignResponse(resp);
                responseStream.WriteAsync(resp);
            }, context.CancellationToken);
        }
    }
}
