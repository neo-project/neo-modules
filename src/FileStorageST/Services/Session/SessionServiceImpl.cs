using Grpc.Core;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Status;
using System;
using System.Threading.Tasks;
using static Neo.FileStorage.Storage.Services.Util.Helper;
using APISessionService = Neo.FileStorage.API.Session.SessionService;

namespace Neo.FileStorage.Storage.Services.Session
{
    public class SessionServiceImpl : APISessionService.SessionServiceBase
    {
        public SessionSignService SignService { get; init; }

        public override Task<CreateResponse> Create(CreateRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    return SignService.Create(request);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(SessionServiceImpl), LogLevel.Debug, e.Message);
                    if (!IsStatusSupported(request)) throw new RpcException(new(StatusCode.Unknown, e.Message));
                    var resp = new CreateResponse();
                    resp.SetStatus(e);
                    SignService.Key.Sign(resp);
                    return resp;
                }
            }, context.CancellationToken);
        }
    }
}
