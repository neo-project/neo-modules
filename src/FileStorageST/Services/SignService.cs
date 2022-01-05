using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Session;
using System;
using System.Security.Cryptography;
using static Neo.FileStorage.API.Status.Helper;
using static Neo.FileStorage.Storage.Services.Util.Helper;

namespace Neo.FileStorage.Storage.Services
{
    public abstract class SignService
    {
        public ECDsa Key { get; init; }

        protected IResponse HandleUnaryRequest(IRequest request, Func<IRequest, IResponse> handler, Func<IResponse> blankResp)
        {
            IResponse resp;
            try
            {
                if (!request.Verify()) throw new Exception($"{nameof(SignService)} invalid {request.GetType()}");
                resp = handler(request);
            }
            catch (Exception e)
            {
                if (!IsStatusSupported(request)) throw;
                resp = blankResp();
                resp.SetStatus(e);
            }
            Key.Sign(resp);
            return resp;
        }

        protected Action<IResponse> HandleServerStreamRequest(IRequest request, Action<IResponse> handler)
        {
            if (!request.Verify())
                throw new Exception($"{nameof(SignService)} invalid {request.GetType()}");
            return resp =>
            {
                Key.Sign(resp);
                handler(resp);
            };
        }

        protected RequestSignStream CreateRequestStreamer(Action<IRequest> sender, Func<IResponse> closer, Func<IResponse> blankResp, Action disposer)
        {
            return new()
            {
                BlankResp = blankResp,
                Key = Key,
                Sender = sender,
                Closer = closer,
                Disposer = disposer,
            };
        }
    }
}
