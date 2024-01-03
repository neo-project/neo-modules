using Neo.Json;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Linq;

namespace Neo.Plugins
{
    public static class WebSocketExtensions
    {
        public static JToken ToJson(this NotifyEventArgs args, bool showTxHash = true)
        {
            var json = new JObject()
            {
                ["scripthash"] = $"{args?.ScriptHash}",
                ["eventname"] = $"{args?.EventName}",
                ["state"] = args?.State?.Count > 0 ?
                    new JArray(args!.State.Select(s => s.ToJson())) :
                    Array.Empty<JToken?>(),
            };

            if (showTxHash)
                json["txhash"] = $"{args?.ScriptContainer?.Hash}";

            return json;
        }

        public static JToken ToJson(this LogEventArgs logEventArgs) =>
            new JObject()
            {
                ["txhash"] = $"{logEventArgs?.ScriptContainer?.Hash}",
                ["contract"] = $"{logEventArgs?.ScriptHash}",
                ["message"] = logEventArgs?.Message,
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
