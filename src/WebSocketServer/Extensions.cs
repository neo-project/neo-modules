using Neo.Json;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Plugins
{
    public static class Extensions
    {
        public static JToken ToJson(this NotifyEventArgs args) =>
            new JObject()
            {
                ["txhash"] = $"{args.ScriptContainer.Hash}",
                ["scripthash"] = $"{args.ScriptHash}",
                ["eventname"] = $"{args.EventName}",
                ["state"] = args.State.ToJson(),
            };
    }
}
