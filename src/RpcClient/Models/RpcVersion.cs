using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcVersion
    {
        public class RpcProtocol
        {
            public uint Network { get; set; }
            public uint MillisecondsPerBlock { get; set; }
            public uint MaxValidUntilBlockIncrement { get; set; }
            public uint MaxTraceableBlocks { get; set; }
        }

        public int TcpPort { get; set; }

        public int WsPort { get; set; }

        public uint Nonce { get; set; }

        public string UserAgent { get; set; }

        public RpcProtocol Protocol { get; } = new();

        public JObject ToJson()
        {
            JObject json = new();
            json["tcpport"] = TcpPort;
            json["wsport"] = WsPort;
            json["nonce"] = Nonce;
            json["useragent"] = UserAgent;
            json["protocol"] = new JObject();
            json["protocol"]["network"] = Protocol.Network;
            json["protocol"]["msperblock"] = Protocol.MillisecondsPerBlock;
            json["protocol"]["maxvaliduntilblockincrement"] = Protocol.MaxValidUntilBlockIncrement;
            json["protocol"]["maxtraceableblocks"] = Protocol.MaxTraceableBlocks;
            return json;
        }

        public static RpcVersion FromJson(JObject json)
        {
            RpcVersion v = new()
            {
                TcpPort = (int)json["tcpport"].AsNumber(),
                WsPort = (int)json["wsport"].AsNumber(),
                Nonce = (uint)json["nonce"].AsNumber(),
                UserAgent = json["useragent"].AsString()
            };

            v.Protocol.Network = (uint)json["protocol"]["network"].AsNumber();
            v.Protocol.MillisecondsPerBlock = (uint)json["protocol"]["msperblock"].AsNumber();
            v.Protocol.MaxValidUntilBlockIncrement = (uint)json["protocol"]["maxvaliduntilblockincrement"].AsNumber();
            v.Protocol.MaxTraceableBlocks = (uint)json["protocol"]["maxtraceableblocks"].AsNumber();
            return v;
        }
    }
}
