using Neo.IO.Json;
using Neo.Network.P2P.Payloads;

namespace Neo.Network.RPC.Models
{
    public class RpcBlock
    {
        public Block Block { get; set; }

        public uint Confirmations { get; set; }

        public UInt256 NextBlockHash { get; set; }

        public JObject ToJson(ProtocolSettings protocolSettings)
        {
            JObject json = Utility.BlockToJson(Block, protocolSettings);
            json["confirmations"] = Confirmations;
            json["nextblockhash"] = NextBlockHash?.ToString();
            return json;
        }

        public static RpcBlock FromJson(JObject json, ProtocolSettings protocolSettings)
        {
            return new RpcBlock
            {
                Block = Utility.BlockFromJson(json, protocolSettings),
                Confirmations = (uint)json["confirmations"].AsNumber(),
                NextBlockHash = json["nextblockhash"] is null ? null : UInt256.Parse(json["nextblockhash"].AsString())
            };
        }
    }
}
