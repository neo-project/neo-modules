using Grpc.Core;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Status;
using System;
using System.Threading.Tasks;
using APIObjectService = Neo.FileStorage.API.Object.ObjectService;
using static Neo.FileStorage.Storage.Services.Util.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Acl
{
    public partial class ObjectServiceImpl : APIObjectService.ObjectServiceBase
    {
        public ObjectSignService SignService { get; init; }
        public AclChecker AclChecker { get; init; }

        public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    AclChecker.CheckRequest(request, Operation.Delete);
                    return SignService.Delete(request, context.CancellationToken);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new DeleteResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        return resp;
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task Get(GetRequest request, IServerStreamWriter<GetResponse> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var info = AclChecker.CheckRequest(request, Operation.Get);
                    SignService.Get(request, resp =>
                    {
                        if (resp.Body?.ObjectPartCase == GetResponse.Types.Body.ObjectPartOneofCase.Init)
                        {
                            AclChecker.EAclCheck(info, resp);
                        }
                        responseStream.WriteAsync(resp).Wait();
                    }, context.CancellationToken);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new GetResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        responseStream.WriteAsync(resp).Wait();
                        return;
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task GetRange(GetRangeRequest request, IServerStreamWriter<GetRangeResponse> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var info = AclChecker.CheckRequest(request, Operation.Getrange);
                    SignService.GetRange(request, resp =>
                    {
                        if (resp.Body is not null)
                            AclChecker.EAclCheck(info, resp);
                        responseStream.WriteAsync(resp);
                    }, context.CancellationToken);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new GetRangeResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        responseStream.WriteAsync(resp).Wait();
                        return;
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<GetRangeHashResponse> GetRangeHash(GetRangeHashRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    AclChecker.CheckRequest(request, Operation.Getrangehash);
                    return SignService.GetRangeHash(request, context.CancellationToken);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new GetRangeHashResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        return resp;
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }

        public override Task<HeadResponse> Head(HeadRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var info = AclChecker.CheckRequest(request, Operation.Head);
                    var resp = SignService.Head(request, context.CancellationToken);
                    if (resp.Body is not null)
                        AclChecker.EAclCheck(info, resp);
                    return resp;
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new HeadResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        return resp;
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }

        public override async Task<PutResponse> Put(IAsyncStreamReader<PutRequest> requestStream, ServerCallContext context)
        {
            IRequestStream next = null;
            PutRequest init = null;
            try
            {
                next = SignService.Put(context.CancellationToken);
                RequestInfo info = null;
                while (await requestStream.MoveNext(context.CancellationToken))
                {
                    var request = requestStream.Current;
                    switch (request.Body.ObjectPartCase)
                    {
                        case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                            info = AclChecker.CheckRequest(request, Operation.Put);
                            init = request;
                            break;
                        case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                            if (init is null) throw new InvalidOperationException($"{nameof(ObjectServiceImpl)} {nameof(Put)} missing init");
                            break;
                        default:
                            throw new FormatException($"{nameof(ObjectServiceImpl)} {nameof(Put)} invalid put request");
                    }
                    if (!next.Send(request)) break;
                }
                while (await requestStream.MoveNext(context.CancellationToken)) { }
                var resp = (PutResponse)next.Close();
                return resp;
            }
            catch (Exception e)
            {
                Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                if (init is not null && IsStatusSupported(init))
                {
                    var resp = new PutResponse();
                    resp.SetStatus(e);
                    SignService.Key.Sign(resp);
                    return resp;
                }
                if (e is OperationCanceledException)
                    throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                if (e is ObjectException oe)
                    throw new RpcException(new(StatusCode.Unknown, oe.Message));
                throw new RpcException(new(StatusCode.Internal, e.Message));
            }
            finally
            {
                next?.Dispose();
            }
        }

        public override Task Search(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var info = AclChecker.CheckRequest(request, Operation.Search);
                    SignService.Search(request, resp =>
                    {
                        if (resp.Body is not null)
                            AclChecker.EAclCheck(info, resp);
                        responseStream.WriteAsync(resp);
                    }, context.CancellationToken);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(ObjectServiceImpl), LogLevel.Warning, e.Message);
                    if (IsStatusSupported(request))
                    {
                        var resp = new SearchResponse();
                        resp.SetStatus(e);
                        SignService.Key.Sign(resp);
                        responseStream.WriteAsync(resp).Wait();
                    }
                    if (e is OperationCanceledException)
                        throw new RpcException(new(StatusCode.Cancelled, "operation cancelled"));
                    if (e is ObjectException oe)
                        throw new RpcException(new(StatusCode.Unknown, oe.Message));
                    throw new RpcException(new(StatusCode.Internal, e.Message));
                }
            }, context.CancellationToken);
        }
    }
}
