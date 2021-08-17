using System;
using System.Threading;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route.Placement;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route
{
    public class LoadRouter : IWriterProvider
    {
        public RemoteLoadAnnounceProvider RemoteProvider { get; init; }
        public RouteBuilder RouteBuilder { get; init; }
        public ILocalInfoSource LocalInfo { get; init; }

        public IWriter InitWriter(CancellationToken cancellation)
        {
            RouteContext context = new(new() { LocalInfo.NodeInfo }, cancellation);
            return new LoadWriter()
            {
                Router = this,
                RouteContext = context,
            };
        }

        public IWriter InitWriter(RouteContext context)
        {
            return new LoadWriter()
            {
                Router = this,
                RouteContext = context,
            };
        }
    }
}
