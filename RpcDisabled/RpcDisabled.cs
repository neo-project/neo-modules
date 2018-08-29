using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Network.RPC;
using System;
using System.Linq;

namespace Neo.Plugins
{
    public class RpcDisabled : Plugin, IRpcPlugin
    {
        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (Settings.Default.DisabledMethods.Contains(method))
                throw new RpcException(-400, "Access denied");
            return null;
        }
    }
}
