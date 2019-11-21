using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Plugins.HttpServer
{
	public class RpcInterceptor : Plugin, IHttpPlugin
	{
		public void ConfigureHttp(IHttpServer server)
        {
            server.AddRequestInterceptor((payload) =>
			{
				var operationName = extractMethod(payload.Context, payload.Body);

				if (string.IsNullOrEmpty(operationName))
				{
					// if it can't find the "method" in the RPC format it is not supposed to be handled by RPC interceptor
					return;
				}

				var id = extractId(payload.Context, payload.Body);

				payload.Data["JSON-RPC-ID"] = id;
				payload.Data["OperationName"] = operationName;

				var parameters = extractParamsAsArray(payload.Context, payload.Body);
				var result = server.CallOperation(payload.Context, null, operationName, parameters);

				payload.Data["ParametersArray"] = parameters;
				payload.Response = result;
			});

			server.AddResponseInterceptor((payload) =>
			{
                if (payload.Data.ContainsKey("JSON-RPC-ID"))
                {
                    var response = new JObject()
                    {
                        ["jsonrpc"] = "2.0",
                        ["result"] = JObject.FromPrimitive(payload.Response)
                    };

					var id = payload.Data["JSON-RPC-ID"] as string;

					if (!string.IsNullOrEmpty(id))
					{
						response["id"] = id;
					}

					payload.Response = response;
				}
			});
		}

		private string extractId(HttpContext context, string body)
		{
			if (HttpMethods.Get.Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
			{
				// GET localhost:10332?id=123
				return context.Request.Query["id"];
			}

			if (string.IsNullOrEmpty(body))
			{
				return null;
			}

			// POST localhost:10332 BODY{ "id": 123 }
			var jobj = JObject.Parse(body);

			if (jobj.ContainsProperty("id"))
			{
				return jobj["id"].AsString();
			}

			return null;
		}

		private string extractMethod(HttpContext context, string body)
		{
			if (HttpMethods.Get.Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
			{
				// GET localhost:10332?method=methodname
				return context.Request.Query["method"];
			}

            if (!string.IsNullOrEmpty(body))
            {
                // POST localhost:10332 BODY{ "method": "methodname" }
                var jobj = JObject.Parse(body);

                if (jobj.ContainsProperty("method"))
                {
                    return jobj["method"].AsString();
                }
            }

			return null;
		}

		private object[] extractParamsAsArray(HttpContext context, string body)
		{
			if (HttpMethods.Post.Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
			{
				// POST localhost:10332 BODY{ "params": [1, "a"] }
				return (object[])JObject.Parse(body)["params"].ToPrimitive();
			}

			// GET localhost:10332?params=[1, "a"]
			string par = context.Request.Query["params"];
			try
			{
				return (object[])JObject.Parse(par).ToPrimitive();
			}
			catch
			{
				// Try in base64
				par = Encoding.UTF8.GetString(Convert.FromBase64String(par));
				return (object[])JObject.Parse(par).ToPrimitive();
			}
		}
	}
}
