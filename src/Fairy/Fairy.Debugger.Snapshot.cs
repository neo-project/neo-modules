using Neo.IO.Json;
using System.Collections.Concurrent;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        readonly ConcurrentDictionary<string, ApplicationDebugger> debugSessionToEngine = new();

        [RpcMethod]
        protected virtual JObject ListDebugSnapshots(JArray _params)
        {
            return debugSessionToEngine.Keys.Select(p => (JString)p).ToArray();
        }

        [RpcMethod]
        protected virtual JObject DeleteDebugSnapshots(JArray _params)
        {
            JObject json = new();
            foreach (var s in _params)
            {
                string key = s.AsString();
                json[key] = debugSessionToEngine.Remove(key, out var session);
                session?.Dispose();
            }
            return json;
        }
    }
}
