using Google.Protobuf;
using Grpc.Core;
using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Cryptography;
using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using Neo.FSNode.Core.Container;
using Neo.FSNode.Services.Object.Acl;
using Neo.FSNode.Services.Object.Delete;
using Neo.FSNode.Services.Object.Get;
using Neo.FSNode.Services.Object.Head;
using Neo.FSNode.Services.Object.Put;
using Neo.FSNode.Services.Object.Range;
using Neo.FSNode.Services.Object.RangeHash;
using Neo.FSNode.Services.Object.Search;
using Neo.FSNode.Services.Object.Sign;
using Neo.FSNode.Services.ObjectManager.Transformer;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Services.ObjectManager.Placement;
using NeoFS.API.v2.Session;
using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.Object
{
    public partial class ObjectServiceImpl : ObjectService.ObjectServiceBase
    {
        private readonly ECDsa key;
        private readonly Storage localStorage;
        private readonly IContainerSource contnainerSource;
        private readonly IInnerRingFetcher innerRingFetcher;
        private readonly INetmapSource netmapSource;
        private readonly IState netmapState;
        private readonly IEAclStorage eAclStorage;
        private readonly Signer signer;
        private readonly DeleteService deleteService;
        private readonly GetService getService;
        private readonly HeadService headService;
        private readonly PutService putService;
        private readonly RangeService rangeService;
        private readonly RangeHashService rangeHashService;
        private readonly SearchService searchService;
        private readonly AclChecker aclChecker;

        public ObjectServiceImpl(IContainerSource container_source, Storage local_storage, IEAclStorage eacl_storage, IState state, IInnerRingFetcher fetcher)
        {
            localStorage = local_storage;
            eAclStorage = eacl_storage;
            contnainerSource = container_source;
            netmapState = state;
            innerRingFetcher = fetcher;
            signer = new Signer();
            deleteService = new DeleteService();
            getService = new GetService();
            headService = new HeadService(new RelationSearcher());
            putService = new PutService();
            rangeService = new RangeService();
            rangeHashService = new RangeHashService();
            searchService = new SearchService();
            var classifier = new Classifier(innerRingFetcher, netmapSource);
            aclChecker = new AclChecker(contnainerSource, localStorage, eAclStorage, classifier, netmapState);
        }

        private ObjectID GetObjectIDFromRequest(IRequest request)
        {
            return request switch
            {
                GetRequest getRequest => getRequest.Body.Address.ObjectId,
                DeleteRequest deleteRequest => deleteRequest.Body.Address.ObjectId,
                HeadRequest headRequest => headRequest.Body.Address.ObjectId,
                GetRangeRequest getRange => getRange.Body.Address.ObjectId,
                GetRangeHashRequest getRangeHashRequest => getRangeHashRequest.Body.Address.ObjectId,
                _ => null,
            };
        }

        private void UseObjectIDFromSession(RequestInfo info, SessionToken session)
        {
            var oid = session?.Body?.Object?.Address?.ObjectId;
            if (oid is null) return;
            info.ObjectID = oid;
        }

        public override Task Get(GetRequest request, IServerStreamWriter<GetResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var cid = aclChecker.GetContainerIDFromRequest(request);
                var info = aclChecker.FindRequestInfo(request, cid, Operation.Get);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            if (!request.VerifyRequest()) throw new RpcException(new Status(StatusCode.Unauthenticated, "verify header failed"));
            return Task.Run(() =>
            {
                var prm = GetPrm.FromRequest(request);
                var obj = getService.Get(prm);
                var resp = new GetResponse
                {
                    Body = new GetResponse.Types.Body
                    {
                        Init = new GetResponse.Types.Body.Types.Init
                        {
                            Header = obj.Header,
                            ObjectId = obj.ObjectId,
                            Signature = obj.Signature,
                        }
                    }
                };
                resp.SignResponse(key);
                responseStream.WriteAsync(resp);
                var payload = obj.Payload.ToByteArray();
                int offset = 0;
                while (offset < obj.Payload.Length)
                {
                    var end = offset + V2Object.ChunkSize;
                    if (payload.Length < end) end = payload.Length;
                    resp = new GetResponse
                    {
                        Body = new GetResponse.Types.Body
                        {
                            Chunk = ByteString.CopyFrom(payload[offset..end]),
                        }
                    };
                    offset = end;
                    resp.SignResponse(key);
                    responseStream.WriteAsync(resp);
                }
            });
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
                var cid = aclChecker.GetContainerIDFromRequest(request);
                var info = aclChecker.FindRequestInfo(request, cid, Operation.Delete);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var prm = DeletePrm.FromRequest(request);
                var result = deleteService.Delete(prm);
                var resp = new DeleteResponse
                {
                    Body = new DeleteResponse.Types.Body { }
                };
                resp.SignResponse(key);
                return resp;
            });
        }

        public override Task<HeadResponse> Head(HeadRequest request, ServerCallContext context)
        {
            RequestInfo info;
            try
            {
                var cid = aclChecker.GetContainerIDFromRequest(request);
                info = aclChecker.FindRequestInfo(request, cid, Operation.Head);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            if (!request.VerifyRequest()) throw new RpcException(new Status(StatusCode.Unauthenticated, " verify failed"));
            var head_prm = HeadPrm.FromRequest(request);
            var obj = headService.Head(head_prm);
            var resp = obj.Header.ToHeadResponse(head_prm.Short);
            resp.SignResponse(key);
            if (!aclChecker.EAclCheck(resp, info)) throw new InvalidOperationException(nameof(Head) + " response extend basic acl failed.");
            return Task.FromResult(resp);
        }


        public override Task GetRange(GetRangeRequest request, IServerStreamWriter<GetRangeResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var cid = aclChecker.GetContainerIDFromRequest(request);
                var info = aclChecker.FindRequestInfo(request, cid, Operation.Getrange);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            var prm = RangePrm.FromRequest(request);
            var head_result = rangeService.Range(prm);
            var resp = new GetRangeResponse
            {
                Body = new GetRangeResponse.Types.Body
                {
                    Chunk = head_result.Chunk,
                }
            };
            resp.SignResponse(key);
            return responseStream.WriteAsync(resp);
        }

        public override Task<GetRangeHashResponse> GetRangeHash(GetRangeHashRequest request, ServerCallContext context)
        {
            try
            {
                var cid = aclChecker.GetContainerIDFromRequest(request);
                var info = aclChecker.FindRequestInfo(request, cid, Operation.Getrangehash);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, e.Message));
            }
            return Task.Run(() =>
            {
                var prm = RangeHashPrm.FromRequest(request);
                var result = rangeHashService.RangeHash(prm);
                var resp = new GetRangeHashResponse
                {
                    Body = new GetRangeHashResponse.Types.Body { }
                };
                resp.SignResponse(key);
                return resp;
            });
        }

        public override Task Search(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var cid = aclChecker.GetContainerIDFromRequest(request);
                var info = aclChecker.FindRequestInfo(request, cid, Operation.Search);
                info.ObjectID = GetObjectIDFromRequest(request);
                UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
                if (!aclChecker.BasicAclCheck(info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " basic acl check failed"));
                if (!aclChecker.EAclCheck(request, info)) throw new RpcException(new Status(StatusCode.PermissionDenied, " eacl check failed"));
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
                resp.SignResponse(key);
                responseStream.WriteAsync(resp);
            });
        }
    }
}
