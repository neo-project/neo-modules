using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Services.Container.Announcement.Route.Placement;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class Router
    {
        public RemoteLoadAnnounceProvider remoteProvider;
        public RouteBuilder RouteBuilder;
        public NodeInfo LocalNodeInfo;

        public LoadWriter InitWriter(object ctx)
        {
            RouteContext context;
            if (ctx is CancellationToken token)
            {
                context = new(new() { LocalNodeInfo }, token);
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
