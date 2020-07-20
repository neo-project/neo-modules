using Neo.IO.Json;
using Neo.Network.P2P.Payloads;

namespace Neo.Network.RPC.Models
{
    public class RpcBlockHeader
    {
        public Header Header { get; set; }

        public uint Confirmations { get; set; }

        public UInt256 NextBlockHash { get; set; }

        public JObject ToJson()
        {
            JObject json = Header.ToJson();
            json["confirmations"] = Confirmations;
            json["nextblockhash"] = NextBlockHash?.ToString();
            return json;
        }

        public static RpcBlockHeader FromJson(JObject json)
        {
            RpcBlockHeader block = new RpcBlockHeader();
            block.Header = Utility.HeaderFromJson(json);
            block.Confirmations = (uint)json["confirmations"].AsNumber();
            block.NextBlockHash = json["nextblockhash"] is null ? null : UInt256.Parse(json["nextblockhash"].AsString());
            return block;
        }
    }
}
