using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Status;
using System;
using System.Security.Cryptography;
using static Neo.FileStorage.Storage.Services.Util.Helper;

namespace Neo.FileStorage.Storage.Services
{
    public sealed class RequestSignStream : IRequestStream
    {
        public bool StatusSupported { get; private set; }
        public Func<IResponse> BlankResp { get; init; }
        public ECDsa Key { get; init; }
        public Action<IRequest> Sender { get; init; }
        public Func<IResponse> Closer { get; init; }
        public Action Disposer { get; init; }
        private Exception error;

        public bool Send(IRequest request)
        {
            if (error is not null) return false;
            StatusSupported = IsStatusSupported(request);
            try
            {
                if (!request.Verify())
                    throw new Exception($"{nameof(RequestSignStream)} invalid {request.GetType()}");
                Sender(request);
                return true;
            }
            catch (Exception e)
            {
                if (!StatusSupported) throw;
                error = e;
                return false;
            }
        }

        public IResponse Close()
        {
            IResponse resp;
            if (error is not null)
            {
                resp = BlankResp();
                resp.SetStatus(error);
            }
            else
            {
                try
                {
                    resp = Closer();
                }
                catch (Exception e)
                {
                    if (!StatusSupported) throw;
                    resp = BlankResp();
                    resp.SetStatus(e);
                }
            }
            Key.Sign(resp);
            return resp;
        }

        public void Dispose()
        {
            Disposer();
        }
    }
}
