using Neo.IO.Json;

namespace Neo.Plugins
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
            json["tcpPort"] = TcpPort;
            json["wsPort"] = WsPort;
            json["nonce"] = Nonce;
            json["useragent"] = UserAgent;
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            RpcVersion version = new RpcVersion();
            version.TcpPort = (int)json["tcpPort"].AsNumber();
            version.WsPort = (int)json["wsPort"].AsNumber();
            version.Nonce = (uint)json["nonce"].AsNumber();
            version.UserAgent = json["useragent"].AsString();
            return version;
        }
    }
}
