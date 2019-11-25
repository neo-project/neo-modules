using Microsoft.AspNetCore.Http;
using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Plugins.HttpServer
{
    public class RestInterceptor : HttpPlugin
    {
        public override void ConfigureHttp(HttpServer server)
        {
            server.AddRequestInterceptor((payload) =>
            {
                var operationName = extractMethod(payload.Context, payload.Body);

                if (string.IsNullOrEmpty(operationName))
                {
                    // if it can't find the "method" in the Rest format it is not supposed to be handled by Rest interceptor
                    return;
                }

                var id = extractId(payload.Context, payload.Body);
                var controllerName = extractController(payload.Context);

                payload.Data["REST-ID"] = id;
                payload.Data["OperationName"] = operationName;
                payload.Data["ControllerName"] = controllerName;

                var parameters = extractParamsAsDictionary(payload.Context, payload.Body);
                var result = server.CallOperation(payload.Context, controllerName, operationName, parameters);

                payload.Data["ParametersDictionary"] = parameters;
                payload.Response = result;
            });

            server.AddResponseInterceptor((payload) =>
            {
                if (payload.Data.ContainsKey("REST-ID"))
                {

                    var response = new JObject()
                    {
                        ["result"] = JObject.FromPrimitive(payload.Response)
                    };

                    var id = payload.Data["REST-ID"] as string;

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
            // ANY localhost:10332/controllername/methodname
            // ANY localhost:10332/methodname

            // skip 1 because we dont need the things before the first bar
            var pathParts = context.Request.Path.Value.Split('/').Skip(1).ToArray();

            return pathParts[pathParts.Length - 1];
        }

        private string extractController(HttpContext context)
        {
            // skip 1 because we dont need the things before the first bar
            var pathParts = context.Request.Path.Value.Split('/').Skip(1).ToArray();

            if (pathParts.Length > 1)
            {
                // ANY localhost:10332/controllername/methodname
                return pathParts[0];
            }

            // ANY localhost:10332/methodname
            // there is no controllername, will use $root
            return null;
        }

        private IDictionary<string, object> extractParamsAsDictionary(HttpContext context, string body)
        {
            if (HttpMethods.Get.Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
            {
                // GET localhost:10332/controllername/methodname?first=1&second=a
                return context.Request.Query.ToDictionary(p => p.Key, p =>
                {
                    var pArray = (object[])p.Value;

                    if (pArray.Length == 1)
                    {
                        return pArray[0];
                    }
                    else
                    {
                        return (object)pArray;
                    }
                });
            }

            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            // POST localhost:10332 BODY{ "first": 1, "second": "a" }
            return (IDictionary<string, object>)JObject.Parse(body).ToPrimitive();
        }

    }
}
