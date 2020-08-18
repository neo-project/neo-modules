#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO.Json;
using Neo.Wallets;
using System.Linq;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        [RpcMethod]
        private JObject ListPlugins(JArray _params)
        {
            return new JArray(Plugin.Plugins
                .OrderBy(u => u.Name)
                .Select(u => new JObject
                {
                    ["name"] = u.Name,
                    ["version"] = u.Version.ToString(),
                    ["interfaces"] = new JArray(u.GetType().GetInterfaces()
                        .Select(p => p.Name)
                        .Where(p => p.EndsWith("Plugin"))
                        .Select(p => (JObject)p))
                }));
        }

        [RpcMethod]
        private JObject ValidateAddress(JArray _params)
        {
            string address = _params[0].AsString();
            JObject json = new JObject();
            UInt160 scriptHash;
            try
            {
                scriptHash = AddressToScriptHash(address);
            }
            catch
            {
                scriptHash = null;
            }
            json["address"] = address;
            json["isvalid"] = scriptHash != null;
            return json;
        }
    }
}
