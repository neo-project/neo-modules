using Neo.Json;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.Plugins
{
    public static class WebSocketExtensions
    {
        public static JToken ToJson(this NotifyEventArgs args) =>
            new JObject()
            {
                ["txhash"] = $"{args.ScriptContainer?.Hash}",
                ["scripthash"] = $"{args?.ScriptHash}",
                ["eventname"] = $"{args.EventName}",
                ["state"] = new JArray(args.State.Select(s => s.ToJson())),
            };

        public static JToken ToJson(this LogEventArgs logEventArgs) =>
            new JObject()
            {
                ["txhash"] = $"{logEventArgs.ScriptContainer?.Hash}",
                ["contract"] = $"{logEventArgs?.ScriptHash}",
                ["message"] = logEventArgs.Message,
            };

        public static void TryCatch<TSource>(this TSource eventObject, Action<TSource> action)
        {
            try
            {
                action(eventObject);
            }
            catch
            {
            }
        }
    }
}
