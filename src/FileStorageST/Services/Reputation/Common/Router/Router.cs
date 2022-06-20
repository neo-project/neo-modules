namespace Neo.FileStorage.Storage.Services.Reputaion.Common.Route
{
    public class Router : IWriterProvider
    {
        public ILocalInfoSource LocalNodeInfoSource { get; init; }
        public IBuilder RouteBuilder { get; init; }
        public IRemoteWriterProvider RemoteWriterProvider { get; init; }

        public IWriter InitWriter(ICommonContext context)
        {
            RouteContext rtx;
            if (context is not RouteContext)
            {
                rtx = new(context, new() { LocalNodeInfoSource.NodeInfo });
            }
            else
            {
                rtx = (RouteContext)context;
            }
            return new TrustWriter
            {
                Router = this,
                RouteContext = rtx,
            };
        }
    }
}
