using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcVersion
    {
        public int TcpPort { get; set; }

        public int WsPort { get; set; }

        public uint Nonce { get; set; }

        public string UserAgent { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["tcpport"] = TcpPort;
            json["wsport"] = WsPort;
            json["nonce"] = Nonce;
            json["useragent"] = UserAgent;
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            RpcVersion version = new RpcVersion();
            version.TcpPort = (int)json["tcpport"].AsNumber();
            version.WsPort = (int)json["wsport"].AsNumber();
            version.Nonce = (uint)json["nonce"].AsNumber();
            version.UserAgent = json["useragent"].AsString();
            return version;
        }
    }
}
