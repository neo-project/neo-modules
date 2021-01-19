using Neo.FSNode.Core.Netmap;
using NeoFS.API.v2.Session;
using V2Version = NeoFS.API.v2.Refs.Version;

namespace Neo.FSNode.Services.Util.Response
{
    public class Service
    {
        private Cfg cfg;
        public Service(Option[] opts)
        {
            var c = Cfg.DefaultCfg;

            foreach (var opt in opts)
            {
                opt(c);
            }
            this.cfg = c;
        }

        public IResponse HandleUnaryRequest(object req, UnaryHandler handler)
        {
            var resp = handler(req);
            Helper.SetMeta(resp, this.cfg);
            return resp;
        }

        public ServerMessageStreamer HandleServerStreamRequest(object req, ServerStreamHandler handler)
        {
            var msgRdr = handler(req);
            return new ServerMessageStreamer(this.cfg, msgRdr);
        }

        public ClientMessageStreamer CreateRequestStreamer(RequestMessageWriter sender, ClientStreamCloser closer)
        {
            return new ClientMessageStreamer(this.cfg, sender, closer);
        }
    }

    public class Cfg
    {
        public V2Version Version { get; set; }
        public IState State { get; set; }

        public static readonly Cfg DefaultCfg = new Cfg() { Version = new V2Version() { Major = V2Version.MajorFieldNumber, Minor = V2Version.MinorFieldNumber } };
    }

    public delegate void Option(Cfg cfg);

    public static class Helper
    {
        public static void SetMeta(this IResponse resp, Cfg cfg)
        {
            var meta = new ResponseMetaHeader() { Version = cfg.Version, Ttl = 1, Epoch = cfg.State.CurrentEpoch() };
            var origin = resp.MetaHeader;
            if (!(origin is null))
                meta.Origin = origin;
            resp.MetaHeader = meta;
        }

        public static Option WithNetworkState(this IState state)
        {
            return (cfg) => cfg.State = state;
        }
    }

}
