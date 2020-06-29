using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.VM;

namespace Neo.Network.RPC.Models
{
    public class RpcTransaction
    {
        public Transaction Transaction { get; set; }

        public UInt256 BlockHash { get; set; }

        public uint? Confirmations { get; set; }

        public ulong? BlockTime { get; set; }

        public VMState? VMState { get; set; }

        public JObject ToJson()
        {
            JObject json = Transaction.ToJson();
            if (Confirmations != null)
            {
                json["block_hash"] = BlockHash.ToString();
                json["confirmations"] = Confirmations;
                json["block_time"] = BlockTime;
                if (VMState != null)
                {
                    json["vm_state"] = VMState;
                }
            }
            return json;
        }

        public static RpcTransaction FromJson(JObject json)
        {
            RpcTransaction transaction = new RpcTransaction();
            transaction.Transaction = Utility.TransactionFromJson(json);
            if (json["confirmations"] != null)
            {
                transaction.BlockHash = UInt256.Parse(json["block_hash"].AsString());
                transaction.Confirmations = (uint)json["confirmations"].AsNumber();
                transaction.BlockTime = (ulong)json["block_time"].AsNumber();
                transaction.VMState = json["vm_state"]?.TryGetEnum<VMState>();
            }
            return transaction;
        }
    }
}
