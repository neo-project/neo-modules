using Neo.IO.Json;
using Neo.SmartContract;
using System;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcInvokeResult
    {
        public string Script { get; set; }

        public VM.VMState State { get; set; }

        public string GasConsumed { get; set; }

        public ContractParameter[] Stack { get; set; }

        public string Tx { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["script"] = Script;
            json["state"] = State;
            json["gas_consumed"] = GasConsumed;
            try
            {
                json["stack"] = new JArray(Stack.Select(p => p.ToJson()));
            }
            catch (InvalidOperationException)
            {
                // ContractParameter.ToJson() may cause InvalidOperationException
                json["stack"] = "error: recursive reference";
            }
            if (!string.IsNullOrEmpty(Tx)) json["tx"] = Tx;
            return json;
        }

        public static RpcInvokeResult FromJson(JObject json)
        {
            RpcInvokeResult invokeScriptResult = new RpcInvokeResult();
            invokeScriptResult.Script = json["script"].AsString();
            invokeScriptResult.State = json["state"].TryGetEnum<VM.VMState>();
            invokeScriptResult.GasConsumed = json["gas_consumed"].AsString();
            try
            {
                invokeScriptResult.Stack = ((JArray)json["stack"]).Select(p => ContractParameter.FromJson(p)).ToArray();
            }
            catch { }
            invokeScriptResult.Tx = json["tx"]?.AsString();
            return invokeScriptResult;
        }
    }

    public class RpcStack
    {
        public string Type { get; set; }

        public string Value { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["type"] = Type;
            json["value"] = Value;
            return json;
        }

        public static RpcStack FromJson(JObject json)
        {
            RpcStack stackJson = new RpcStack();
            stackJson.Type = json["type"].AsString();
            stackJson.Value = json["value"].AsString();
            return stackJson;
        }
    }
}
