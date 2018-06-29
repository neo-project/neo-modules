using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Network.RPC;
using System.IO;

namespace Neo.Plugins
{
    public class LogReader : Plugin, IRpcPlugin
    {
        public override string Name => "ApplicationLogs";

        public LogReader()
        {
            System.ActorSystem.ActorOf(Logger.Props(System.Blockchain));
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            string path = Path.Combine(Settings.Default.Path, $"{hash}.json");
            return File.Exists(path)
                ? JObject.Parse(File.ReadAllText(path))
                : throw new RpcException(-100, "Unknown transaction");
        }
    }
}
