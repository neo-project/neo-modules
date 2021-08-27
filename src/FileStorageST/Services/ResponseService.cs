
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Core;
using System;
using FSVersion = Neo.FileStorage.API.Refs.Version;

namespace Neo.FileStorage.Storage.Services
{
    public class ResponseService
    {
        public IEpochSource EpochSource { get; init; }

        public void SetMeta(IResponse response)
        {
            var meta = new ResponseMetaHeader()
            {
                Version = FSVersion.SDKVersion(),
                Ttl = 1,// TODO: calculate ttl
                Epoch = EpochSource.CurrentEpoch,
            };
            if (response.MetaHeader is not null)
            {
                meta.Origin = response.MetaHeader;// TODO: what if origin is set by local server?
            }
            response.MetaHeader = meta;
        }

        public IResponse HandleUnaryRequest(IRequest request, Func<IRequest, IResponse> handler)
        {
            var resp = handler(request);
            SetMeta(resp);
            return resp;
        }

        protected Action<IResponse> HandleServerStreamRequest(Action<IResponse> handler)
        {
            return resp =>
            {
                SetMeta(resp);
                handler(resp);
            };
        }

        protected RequestResponseStream CreateRequestStream(Action<IRequest> sender, Func<IResponse> closer, Action disposer)
        {
            return new()
            {
                ResponseService = this,
                Sender = sender,
                Closer = closer,
                Disposer = disposer,
            };
        }
    }

    public sealed class RequestResponseStream : IRequestStream
    {
        public ResponseService ResponseService { get; init; }
        public Action<IRequest> Sender { get; init; }
        public Func<IResponse> Closer { get; init; }
        public Action Disposer { get; init; }

        public void Send(IRequest request)
        {
            Sender(request);
        }

        public IResponse Close()
        {
            var resp = Closer();
            ResponseService.SetMeta(resp);
            return resp;
        }

        public void Dispose()
        {
            Disposer();
        }
    }
}
