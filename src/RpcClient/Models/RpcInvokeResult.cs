using Neo.IO.Json;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcInvokeResult
    {
        public string Script { get; set; }

        public VM.VMState State { get; set; }

        public string GasConsumed { get; set; }

        public StackItem[] Stack { get; set; }

        public string Tx { get; set; }

        public string Exception { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["script"] = Script;
            json["state"] = State;
            json["gasconsumed"] = GasConsumed;
            if (!string.IsNullOrEmpty(Exception))
                json["exception"] = Exception;
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
            RpcInvokeResult invokeScriptResult = new RpcInvokeResult
            {
                Script = json["script"].AsString(),
                State = json["state"].TryGetEnum<VM.VMState>(),
                GasConsumed = json["gasconsumed"].AsString()
            };
            if (json.ContainsProperty("exception"))
                invokeScriptResult.Exception = json["exception"]?.AsString();
            try
            {
                invokeScriptResult.Stack = ((JArray)json["stack"]).Select(p => Utility.StackItemFromJson(p)).ToArray();
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
            return new RpcStack
            {
                Type = json["type"].AsString(),
                Value = json["value"].AsString()
            };
        }
    }
}
