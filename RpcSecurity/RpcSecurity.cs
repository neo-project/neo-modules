using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using Neo.Network.RPC;
using System;
using System.Linq;
using System.Text;

namespace Neo.Plugins
{
    public class RpcSecurity : Plugin, IRpcPlugin
    {
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
            if (!CheckAuth(context) || Settings.Default.DisabledMethods.Contains(method))
                throw new RpcException(-400, "Access denied");
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }

        private bool CheckAuth(HttpContext context)
        {
            if (string.IsNullOrEmpty(Settings.Default.RpcUser))
            {
                return true;
            }

            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Restricted\"";

            string reqauth = context.Request.Headers["Authorization"];
            string authstring = null;
            try
            {
                authstring = Encoding.UTF8.GetString(Convert.FromBase64String(reqauth.Replace("Basic ", "").Trim()));
            }
            catch
            {
                return false;
            }

            string[] authvalues = authstring.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (authvalues.Length < 2)
                return false;

            return authvalues[0] == Settings.Default.RpcUser && authvalues[1] == Settings.Default.RpcPass;
        }
    }
}
