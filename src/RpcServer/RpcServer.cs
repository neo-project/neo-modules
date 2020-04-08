using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo.IO;
using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public sealed partial class RpcServer : Plugin
    {
        private static readonly Dictionary<string, Func<JArray, JObject>> methods = new Dictionary<string, Func<JArray, JObject>>();
        private IWebHost host;

        public RpcServer()
        {
            RegisterMethods(this);
        }

        private bool CheckAuth(HttpContext context)
        {
            if (string.IsNullOrEmpty(Settings.Default.RpcUser)) return true;

            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Restricted\"";

            string reqauth = context.Request.Headers["Authorization"];
            string authstring;
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

        protected override void Configure()
        {
            IConfigurationSection config = null;
            int remainingTimes = 3;
            while (remainingTimes > 0 && config == null)
            {
                try
                {
                    config = GetConfiguration();
                }
                catch (FormatException)
                {
                    remainingTimes--;
                    Thread.Sleep(10);
                }
            }
            if (config == null) throw new FormatException();
            Settings.Load(config);
        }

        private static JObject CreateErrorResponse(JObject id, int code, string message, JObject data = null)
        {
            JObject response = CreateResponse(id);
            response["error"] = new JObject();
            response["error"]["code"] = code;
            response["error"]["message"] = message;
            if (data != null)
                response["error"]["data"] = data;
            return response;
        }

        private static JObject CreateResponse(JObject id)
        {
            JObject response = new JObject();
            response["jsonrpc"] = "2.0";
            response["id"] = id;
            return response;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (host != null)
            {
                host.Dispose();
                host = null;
            }
        }

        protected override void OnPluginsLoaded()
        {
            host = new WebHostBuilder().UseKestrel(options => options.Listen(Settings.Default.BindAddress, Settings.Default.Port, listenOptions =>
            {
                // Default value is unlimited
                options.Limits.MaxConcurrentConnections = Settings.Default.MaxConcurrentConnections;
                // Default value is 2 minutes
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(1);
                // Default value is 30 seconds
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);

                if (string.IsNullOrEmpty(Settings.Default.SslCert)) return;
                listenOptions.UseHttps(Settings.Default.SslCert, Settings.Default.SslCertPassword, httpsConnectionAdapterOptions =>
                {
                    if (Settings.Default.TrustedAuthorities is null || Settings.Default.TrustedAuthorities.Length == 0)
                        return;
                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                    {
                        if (err != SslPolicyErrors.None)
                            return false;
                        X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return Settings.Default.TrustedAuthorities.Contains(authority.Thumbprint);
                    };
                });
            }))
            .Configure(app =>
            {
                app.UseResponseCompression();
                app.Run(ProcessAsync);
            })
            .ConfigureServices(services =>
            {
                services.AddResponseCompression(options =>
                {
                    // options.EnableForHttps = false;
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/json");
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });
            })
            .Build();

            host.Start();
        }

        private async Task ProcessAsync(HttpContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            context.Response.Headers["Access-Control-Max-Age"] = "31536000";
            if (context.Request.Method != "GET" && context.Request.Method != "POST") return;
            JObject request = null;
            if (context.Request.Method == "GET")
            {
                string jsonrpc = context.Request.Query["jsonrpc"];
                string id = context.Request.Query["id"];
                string method = context.Request.Query["method"];
                string _params = context.Request.Query["params"];
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(method) && !string.IsNullOrEmpty(_params))
                {
                    try
                    {
                        _params = Encoding.UTF8.GetString(Convert.FromBase64String(_params));
                    }
                    catch (FormatException) { }
                    request = new JObject();
                    if (!string.IsNullOrEmpty(jsonrpc))
                        request["jsonrpc"] = jsonrpc;
                    request["id"] = id;
                    request["method"] = method;
                    request["params"] = JObject.Parse(_params);
                }
            }
            else if (context.Request.Method == "POST")
            {
                using StreamReader reader = new StreamReader(context.Request.Body);
                try
                {
                    request = JObject.Parse(reader.ReadToEnd());
                }
                catch (FormatException) { }
            }
            JObject response;
            if (request == null)
            {
                response = CreateErrorResponse(null, -32700, "Parse error");
            }
            else if (request is JArray array)
            {
                if (array.Count == 0)
                {
                    response = CreateErrorResponse(request["id"], -32600, "Invalid Request");
                }
                else
                {
                    response = array.Select(p => ProcessRequest(context, p)).Where(p => p != null).ToArray();
                }
            }
            else
            {
                response = ProcessRequest(context, request);
            }
            if (response == null || (response as JArray)?.Count == 0) return;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response.ToString(), Encoding.UTF8);
        }

        private JObject ProcessRequest(HttpContext context, JObject request)
        {
            if (!request.ContainsProperty("id")) return null;
            if (!request.ContainsProperty("method") || !request.ContainsProperty("params") || !(request["params"] is JArray))
            {
                return CreateErrorResponse(request["id"], -32600, "Invalid Request");
            }
            JObject response = CreateResponse(request["id"]);
            try
            {
                string method = request["method"].AsString();
                if (!CheckAuth(context) || Settings.Default.DisabledMethods.Contains(method))
                    throw new RpcException(-400, "Access denied");
                if (!methods.TryGetValue(method, out var func))
                    throw new RpcException(-32601, "Method not found");
                response["result"] = func((JArray)request["params"]);
                return response;
            }
            catch (FormatException)
            {
                return CreateErrorResponse(request["id"], -32602, "Invalid params");
            }
            catch (IndexOutOfRangeException)
            {
                return CreateErrorResponse(request["id"], -32602, "Invalid params");
            }
            catch (Exception ex)
            {
#if DEBUG
                return CreateErrorResponse(request["id"], ex.HResult, ex.Message, ex.StackTrace);
#else
                return CreateErrorResponse(request["id"], ex.HResult, ex.Message);
#endif
            }
        }

        public static void RegisterMethods(object handler)
        {
            foreach (MethodInfo method in handler.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                RpcMethodAttribute attribute = method.GetCustomAttribute<RpcMethodAttribute>();
                if (attribute is null) continue;
                string name = string.IsNullOrEmpty(attribute.Name) ? method.Name.ToLowerInvariant() : attribute.Name;
                methods[name] = (Func<JArray, JObject>)method.CreateDelegate(typeof(Func<JArray, JObject>), handler);
            }
        }
    }
}
