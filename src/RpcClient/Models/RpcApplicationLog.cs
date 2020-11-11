using Neo.IO.Json;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcApplicationLog
    {
        public UInt256 TxId { get; set; }

        public UInt256 BlockHash { get; set; }

        public List<Execution> Executions { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            if (TxId != null)
                json["txid"] = TxId.ToString();
            if (BlockHash != null)
                json["blockhash"] = BlockHash.ToString();
            json["executions"] = Executions.Select(p => p.ToJson()).ToArray();
            return json;
        }

        public static RpcApplicationLog FromJson(JObject json)
        {
            return new RpcApplicationLog
            {
                TxId = json["txid"] is null ? null : UInt256.Parse(json["txid"].AsString()),
                BlockHash = json["blockhash"] is null ? null : UInt256.Parse(json["blockhash"].AsString()),
                Executions = ((JArray)json["executions"]).Select(p => Execution.FromJson(p)).ToList(),
            };
        }
    }

    public class Execution
    {
        public TriggerType Trigger { get; set; }

        public VMState VMState { get; set; }

        public long GasConsumed { get; set; }

        public List<StackItem> Stack { get; set; }

        public List<RpcNotifyEventArgs> Notifications { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["trigger"] = Trigger;
            json["vmstate"] = VMState;
            json["gasconsumed"] = GasConsumed.ToString();
            json["stack"] = Stack.Select(q => q.ToJson()).ToArray();
            json["notifications"] = Notifications.Select(q => q.ToJson()).ToArray();
            return json;
        }

        public static Execution FromJson(JObject json)
        {
            return new Execution
            {
                Trigger = json["trigger"].TryGetEnum<TriggerType>(),
                VMState = json["vmstate"].TryGetEnum<VMState>(),
                GasConsumed = long.Parse(json["gasconsumed"].AsString()),
                Stack = ((JArray)json["stack"]).Select(p => Utility.StackItemFromJson(p)).ToList(),
                Notifications = ((JArray)json["notifications"]).Select(p => RpcNotifyEventArgs.FromJson(p)).ToList()
            };
        }
    }

    public class RpcNotifyEventArgs
    {
        public UInt160 Contract { get; set; }

        public string EventName { get; set; }

        public StackItem State { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["contract"] = Contract.ToString();
            json["eventname"] = EventName;
            json["state"] = State.ToJson();
            return json;
        }

        public static RpcNotifyEventArgs FromJson(JObject json)
        {
            return new RpcNotifyEventArgs
            {
                Contract = json["contract"].ToScriptHash(),
                EventName = json["eventname"].AsString(),
                State = Utility.StackItemFromJson(json["state"])
            };
        }
    }
}
