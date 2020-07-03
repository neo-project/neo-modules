using Neo.IO.Json;
using Neo.SmartContract;
using Neo.VM;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcApplicationLog
    {
        public UInt256 TxId { get; set; }

        public TriggerType Trigger { get; set; }

        public VMState VMState { get; set; }

        public long GasConsumed { get; set; }

        public List<ContractParameter> Stack { get; set; }

        public List<RpcNotifyEventArgs> Notifications { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["txid"] = TxId?.ToString();
            json["trigger"] = Trigger;
            json["vmstate"] = VMState;
            json["gasconsumed"] = GasConsumed.ToString();
            json["stack"] = Stack.Select(q => q.ToJson()).ToArray();
            json["notifications"] = Notifications.Select(q => q.ToJson()).ToArray();
            return json;
        }

        public static RpcApplicationLog FromJson(JObject json)
        {
            RpcApplicationLog log = new RpcApplicationLog();
            log.TxId = json["txid"] is null ? null : UInt256.Parse(json["txid"].AsString());
            log.Trigger = json["trigger"].TryGetEnum<TriggerType>();
            log.VMState = json["vmstate"].TryGetEnum<VMState>();
            log.GasConsumed = long.Parse(json["gasconsumed"].AsString());
            log.Stack = ((JArray)json["stack"]).Select(p => ContractParameter.FromJson(p)).ToList();
            log.Notifications = ((JArray)json["notifications"]).Select(p => RpcNotifyEventArgs.FromJson(p)).ToList();
            return log;
        }
    }

    public class RpcNotifyEventArgs
    {
        public UInt160 Contract { get; set; }

        public string EventName { get; set; }

        public ContractParameter State { get; set; }

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
                Contract = UInt160.Parse(json["contract"].AsString()),
                EventName = json["eventname"].AsString(),
                State = ContractParameter.FromJson(json["state"])
            };
        }
    }
}
