using Microsoft.AspNetCore.Http;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Network.RPC;
using System.IO;

namespace Neo.Plugins
{
    public class LogReader : Plugin, IRpcPlugin
    {
        private readonly DB db;

        public override string Name => "ApplicationLogs";

        public LogReader()
        {
            db = DB.Open(Path.GetFullPath(Settings.Default.Path), new Options { CreateIfMissing = true });
        }

        public override void OnNeoSystemInitialized()
        {
            System.ActorSystem.ActorOf(Logger.Props(System.Blockchain, db));
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(value.ToString());
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
