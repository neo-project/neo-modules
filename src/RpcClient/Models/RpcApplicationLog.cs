using Neo.IO.Json;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcApplicationLog
    {
        public UInt256 TxHash { get; set; }

        public TriggerType Trigger { get; set; }

        public VMState VMState { get; set; }

        public long GasConsumed { get; set; }

        public List<StackItem> Stack { get; set; }

        public List<NotifyEventArgs> Notifications { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["txid"] = TxHash.ToString();
            json["trigger"] = Trigger;
            json["vmstate"] = VMState;
            json["gas_consumed"] = GasConsumed.ToString();
            try
            {
                json["stack"] = Stack.Select(q => q.ToParameter().ToJson()).ToArray();
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            json["notifications"] = Notifications.Select(q =>
            {
                JObject notification = new JObject();
                notification["contract"] = q.ScriptHash.ToString();
                try
                {
                    notification["state"] = q.State.ToParameter().ToJson();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }
                return notification;
            }).ToArray();
            return json;
        }

        public static RpcApplicationLog FromJson(JObject json)
        {
            RpcApplicationLog log = new RpcApplicationLog();
            log.TxHash = UInt256.Parse(json["txid"].AsString());
            log.Trigger = json["trigger"].TryGetEnum<TriggerType>();
            log.VMState = json["vmstate"].TryGetEnum<VMState>();
            log.GasConsumed = long.Parse(json["gas_consumed"].AsString());

            try
            {
                log.Stack = ((JArray)json["stack"]).Select(p => ContractParameter.FromJson(p).ToStackItem()).ToList();
            }
            catch (Exception) { }

            log.Notifications = new List<NotifyEventArgs>();
            foreach (var notifiy in (JArray)json["notifications"])
            {
                UInt160 scriptHash = UInt160.Parse(notifiy["contract"].AsString());
                StackItem state = null;
                try
                {
                    state = ContractParameter.FromJson(notifiy["state"]).ToStackItem();
                }
                catch (Exception) { }
                log.Notifications.Add(new NotifyEventArgs(null, scriptHash, state));
            }

            return log;
        }
    }
}
