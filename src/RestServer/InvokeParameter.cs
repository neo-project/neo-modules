using Neo.IO.Json;

namespace Neo.Plugins
{
    public class InvokeFunctionParameter
    {
        public string ScriptHash { get; set; }
        public string Operation { get; set; }
        public RpcStack[] Params { get; set; }
    }

    public class InvokeScriptParameter
    {
        public string Script { get; set; }
        public string[] Hashes { get; set; }
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
