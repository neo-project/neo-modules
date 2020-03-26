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
            json["tcp_port"] = TcpPort;
            json["ws_port"] = WsPort;
            json["nonce"] = Nonce;
            json["user_agent"] = UserAgent;
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            RpcVersion version = new RpcVersion();
            version.TcpPort = (int)json["tcp_port"].AsNumber();
            version.WsPort = (int)json["ws_port"].AsNumber();
            version.Nonce = (uint)json["nonce"].AsNumber();
            version.UserAgent = json["user_agent"].AsString();
            return version;
        }
    }
}
