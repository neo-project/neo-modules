
using System;
using System.Threading;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Services.Container.Announcement.Control;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class Router
    {
        public IRemoteWriterProvider RemoteWriterProvider;
        public IBuilder RouteBuilder;
        public NodeInfo LocalServerInfo;

        public IWriter InitWriter(object ctx)
        {
            RouteContext context;
            if (ctx is CancellationToken token)
            {
                context = new()
                {
                    Cancellation = token,
                    PassedRoute = new() { LocalServerInfo },
                };
            }
            else if (ctx is RouteContext c)
            {
                context = c;
            }
            else
                throw new ArgumentException($"invalid type, expect typeof {nameof(CancellationToken)} or {nameof(RouteContext)}, actual {ctx.GetType()}");
            return new LoadWriter()
            {
                Router = this,
                RouteContext = context,
            };
        }
    }
}
