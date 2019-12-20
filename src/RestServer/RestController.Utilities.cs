#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Microsoft.AspNetCore.Mvc;
using Neo.IO.Json;
using Neo.Wallets;
using System.Linq;

namespace Neo.Plugins
{
    partial class RestController
    {
        /// <summary>
        /// Get plugins loaded by the node
        /// </summary>
        /// <returns></returns>
        [HttpGet("network/localnode/plugins")]
        public IActionResult ListPlugins()
        {
            JArray json = new JArray(RestServer.Plugins
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
            return FormatJson(json);
        }

        /// <summary>
        /// Verify whether the address is a correct NEO address	    
        /// </summary>
        /// <param name="address">address to be veirifed</param>
        /// <returns></returns>
        [HttpGet("wallets/verifyingaddress/{address}")]
        public IActionResult ValidateAddress(string address)
        {
            JObject json = new JObject();
            UInt160 scriptHash;
            try
            {
                scriptHash = address.ToScriptHash();
            }
            catch
            {
                scriptHash = null;
            }
            json["address"] = address;
            json["isvalid"] = scriptHash != null;
            return FormatJson(json);
        }
    }
}
