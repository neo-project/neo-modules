using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcVersion
    {
        public uint Magic { get; set; }

        public int TcpPort { get; set; }

        public int WsPort { get; set; }

        public uint Nonce { get; set; }

        public string UserAgent { get; set; }


        public JObject ToJson()
        {
            JObject json = new JObject();
            json["magic"] = Magic;
            json["tcpport"] = TcpPort;
            json["wsport"] = WsPort;
            json["nonce"] = Nonce;
            json["useragent"] = UserAgent;
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            return new RpcVersion
            {
                Magic = (uint)json["magic"].AsNumber(),
                TcpPort = (int)json["tcpport"].AsNumber(),
                WsPort = (int)json["wsport"].AsNumber(),
                Nonce = (uint)json["nonce"].AsNumber(),
                UserAgent = json["useragent"].AsString()
            };
        }
    }
}
