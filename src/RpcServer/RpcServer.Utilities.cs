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
            return new JArray(Plugins
                .OrderBy(u => u.Name)
                .Select(u => new RpcPlugin
                {
                    Name = u.Name,
                    Version = u.Version.ToString(),
                    Interfaces = u.GetType()
                        .GetInterfaces()
                        .Select(p => p.Name)
                        .Where(p => p.EndsWith("Plugin"))
                        .ToArray()
                }.ToJson()));
        }

        [RpcMethod]
        private JObject ValidateAddress(JArray _params)
        {
            string address = _params[0].AsString();
            bool isValid = true;
            try
            {
                address.ToScriptHash();
            }
            catch
            {
                isValid = false;
            }

            return new RpcValidateAddressResult
            {
                Address = address,
                IsValid = isValid

            }.ToJson();
        }
    }
}
