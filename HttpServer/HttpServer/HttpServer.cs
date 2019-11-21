using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Neo.IO.Json;


namespace Neo.Plugins.HttpServer
{
    using Interceptor = Action<IHttpOperationPayload>;

    public class HttpServer : Plugin, IHttpServer
    {
        private const int MaxPostValue = 1024 * 1024 * 2;

        // WEB HOST HANDLING THE SERVER
        private IWebHost _host;

        // SETTINGS PROVIDED BY JSON FILE
        private HttpServerSettings _settings;

        // BINDED OPERATIONS TO BE CALLED WHEN REQUESTS MATCH IT'S SIGNATURE
        private IDictionary<string, IDictionary<string, OperationTargetAndMethod>> _operations = new Dictionary<string, IDictionary<string, OperationTargetAndMethod>>();

        // INJECTED PARAMETERS FOR OPERATIONS: When a controller needs a parameter of a specific TYPE that was injected it will get it automatically
        private IDictionary<Type, Func<HttpContext, object>> _specialParameterInjectors = new Dictionary<Type, Func<HttpContext, object>>();

        // REQUEST INTERCEPTORS: Actions that will be called to handle the request, can read, modify and abort the request and response. Can be used to call the operation aswell
        private List<Interceptor> _requestInterceptors = new List<Interceptor>();

        // RESPONSE INTERCEPTORS: Actions that will be called to handle the response, can read and modify request and response
        private List<Interceptor> _responseInterceptors = new List<Interceptor>();

        public override string Name => "HttpServer";

        public override void Configure()
        {
            _settings = new HttpServerSettings(GetConfiguration());
            InjectSpecialParameter(ctx => ctx);
            InjectSpecialParameter(ctx => _settings);

            RegisterDefaultInterceptors();

            Start();
        }

        public override void OnPluginsLoaded()
        {
            ConfigurePlugins();
        }

        public async void ConfigurePlugins()
        {
            await Task.Delay(500);
            // calls the plugins to configure itself
            foreach (IHttpPlugin plugin in Plugin.HttpPlugins.ToList())
            {
                plugin.ConfigureHttp(this);
            }
        }

        /// <summary>
        /// Starts the server
        /// </summary>
        public void Start()
        {
            // Check started

            if (_host != null)
            {
                Log("Http server already started");
                return;
            }

            if (_settings.ListenEndPoint == null)
            {
                Log("ListenEndPoint not present on config file! Aborting Http Server Startup.");
                return;
            }

            _host = new WebHostBuilder().UseKestrel(options => options.Listen(_settings.ListenEndPoint, listenOptions =>
            {
                // Config SSL

                if (_settings.Ssl != null && _settings.Ssl.IsValid)
                    listenOptions.UseHttps(_settings.Ssl.Path, _settings.Ssl.Password, httpsConnectionAdapterOptions =>
                    {
                        if (_settings.TrustedAuthorities is null || _settings.TrustedAuthorities.Length == 0)
                        {
                            return;
                        }

                        httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                        {
                            if (err != SslPolicyErrors.None)
                            {
                                return false;
                            }

                            X509Certificate2 authority = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                            return _settings.TrustedAuthorities.Contains(authority.Thumbprint);
                        };
                    });
            }))
            .Configure(app =>
            {
                app.UseResponseCompression();
                app.Run(ProcessRequest);
            })
            .ConfigureServices(services =>
            {
                services.AddResponseCompression(options =>
                {
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });
            })
            .Build();

            _host.Start();
            Log($"Http server started on {_settings.ListenEndPoint.Address}:{_settings.ListenEndPoint.Port}");
        }

        private async Task ProcessRequest(HttpContext context)
        {
            var payload = new HttpOperationPayload
            {
                Context = context
            };

            try
            {
                CallRequestInterceptors(payload);
                CallResponseInterceptors(payload);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                HandleException(payload, ex.HResult, ex.Message, ex.StackTrace);
            }

            if (payload.Response != null)
            {
                await payload.Context.Response.WriteAsync(payload.Response.ToString(), Encoding.UTF8);
            }
        }

        private void HandleException(HttpOperationPayload payload, int code, string message, string stacktrace = null)
        {
            var errorResp = new JObject
            {
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            };

            if (stacktrace != null)
            {
                errorResp["error"]["data"] = stacktrace;
            }

            payload.Response = errorResp;
        }

        private void RegisterDefaultInterceptors()
        {
            // abort requests with unallowed IPs
            AddRequestInterceptor((payload) =>
            {
                if (_settings.IpBlacklist != null && _settings.IpBlacklist.Contains(payload.Context.Connection.RemoteIpAddress))
                {
                    Log("Unauthorized request " + payload.Context.Connection.RemoteIpAddress, LogLevel.Warning);

                    payload.AbortRequest = true;
                    payload.Data["IP_NOT_ALLOWED"] = true;

                    payload.Context.Response.StatusCode = 401;

                    var response = (payload.Response as JObject) ?? new JObject();
                    response["error"] = new JObject
                    {
                        ["code"] = 401,
                        ["message"] = "Forbidden"
                    };

                    payload.Response = response;
                }
            });

            // read POST body or abort the request if the body is too long
            AddRequestInterceptor((payload) =>
            {
                if (HttpMethods.Post.Equals(payload.Context.Request.Method, StringComparison.OrdinalIgnoreCase))
                {
                    if (!payload.Context.Request.ContentLength.HasValue || payload.Context.Request.ContentLength > MaxPostValue)
                    {
                        Log("Unauthorized request " + payload.Context.Connection.RemoteIpAddress, LogLevel.Warning);

                        payload.AbortRequest = true;
                        payload.Data["BODY_SIZE_TOO_BIG"] = true;

                        var response = (payload.Response as JObject) ?? new JObject();
                        response["error"] = new JObject
                        {
                            ["code"] = -32700,
                            ["message"] = "The post body is too long, max size is " + MaxPostValue
                        };

                        payload.Response = response;
                    }
                    else
                    {
                        payload.Body = new StreamReader(payload.Context.Request.Body).ReadToEnd();
                    }
                }
            });

            // add default headers
            AddResponseInterceptor((payload) =>
            {
                payload.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                payload.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
                payload.Context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
                payload.Context.Response.Headers["Access-Control-Max-Age"] = "31536000";
                payload.Context.Response.ContentType = "application/json";
            });
        }

        private IHttpOperationPayload CallRequestInterceptors(IHttpOperationPayload payload)
        {
            foreach (var interc in _requestInterceptors)
            {
                interc.Invoke(payload);

                if (payload.AbortRequest)
                {
                    break;
                }
            }

            return payload;
        }

        private IHttpOperationPayload CallResponseInterceptors(IHttpOperationPayload payload)
        {
            foreach (var interc in _responseInterceptors)
            {
                interc.Invoke(payload);
            }

            return payload;
        }

        /// <summary>
        /// Stops the server
        /// </summary>
        public void Stop()
        {
            if (_host == null)
            {
                Log("Http server already stopped");
                return;
            }

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            Log("Http server stopped");
        }

        /// <summary>
        /// Free resources
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Register an operation that can be called by the server.
        /// Usage:
        /// server.BindOperation("controllerName", "operationName", new Func<int, bool>(MyMethod));
        /// </summary>
        /// <param name="controllerName">controller name is used to organize many operations in a group</param>
        /// <param name="operationName">operation name</param>
        /// <param name="anyMethod">the method to be called when the operation is called</param>
        public void BindOperation(string controllerName, string operationName, Delegate anyMethod)
        {
            BindOperation(controllerName, operationName, anyMethod.Target, anyMethod.Method);
        }

        /// <summary>
        /// Register an operation that can be called by the server.
        /// Usage:
        /// server.BindOperation("controllerName", "operationName", new Func<int, bool>(MyMethod));
        /// </summary>
        /// <param name="controllerName">controller name is used to organize many operations in a group</param>
        /// <param name="operationName">operation name</param>
        /// <param name="target">caller object of the method</param>
        /// <param name="anyMethod">the method to be called when the operation is called</param>
        public void BindOperation(string controllerName, string operationName, object target, MethodInfo anyMethod)
        {
            controllerName = controllerName ?? "$root";

            if (!_operations.ContainsKey(controllerName))
            {
                _operations.Add(controllerName, new Dictionary<string, OperationTargetAndMethod>());
            }

            var controller = _operations[controllerName];

            var callerAndMethod = new OperationTargetAndMethod()
            {
                Target = target,
                Method = anyMethod
            };

            if (!controller.ContainsKey(operationName))
            {
                controller.Add(operationName, callerAndMethod);
            }
            else
            {
                controller[operationName] = callerAndMethod;
            }
        }

        /// <summary>
        /// Register many operations organized in a controller class,
        /// The operations should be methods annotated with [HttpMethod] or [HttpMethod("methodName")]
        /// </summary>
        /// <typeparam name="T">the controller class</typeparam>
        public void BindController<T>() where T : new()
        {
            var controller = typeof(T);
            var controllerInstance = new T();
            BindController(controller, controllerInstance);
        }

        /// <summary>
        /// Register many operations organized in a controller class,
        /// The operations should be methods annotated with [HttpMethod] or [HttpMethod("methodName")]
        /// </summary>
        /// <param name="controller">the controller class</param>
        public void BindController(Type controller)
        {
            var controllerInstance = Activator.CreateInstance(controller);
            BindController(controller, controllerInstance);
        }

        /// <summary>
        /// Register many operations organized in a controller class,
        /// The operations should be methods annotated with [HttpMethod] or [HttpMethod("methodName")]
        /// </summary>
        /// <param name="controller">the controller class</param>
        private void BindController(Type controller, object controllerInstance)
        {
            var controllerName = controller.Name;
            var controllerAttr = controller.GetCustomAttributes<HttpControllerAttribute>(false).FirstOrDefault();

            if (controllerAttr != null && !string.IsNullOrEmpty(controllerAttr.Path))
            {
                controllerName = controllerAttr.Path;
            }

            var methods = controller.GetMethods()
                .Select(m => (Method: m, Attribute: m.GetCustomAttributes<HttpMethodAttribute>(false).FirstOrDefault()));

            foreach (var i in methods)
            {
                var name = i.Method.Name;

                if (i.Attribute != null && !string.IsNullOrEmpty(i.Attribute.Path))
                {
                    name = i.Attribute.Path;
                }

                BindOperation(controllerName, name, controllerInstance, i.Method);
            }
        }

        /// <summary>
        /// Calls the server operation
        /// </summary>
        /// <param name="controllerName">the controller name</param>
        /// <param name="operationName">the operation name</param>
        /// <param name="parameters">all parameters expected by the operation</param>
        /// <returns>the return of the operation, not casted</returns>
        public object CallOperation(HttpContext context, string controllerName, string operationName, params object[] parameters)
        {
            controllerName = controllerName ?? "$root";

            if (_operations.ContainsKey(controllerName) && _operations[controllerName].ContainsKey(operationName))
            {
                var method = _operations[controllerName][operationName];
                var methodParameters = method.Method.GetParameters();
                List<object> paramsList = new List<object>();

                var m = 0;
                var offsetM = 0;
                while (m + offsetM < methodParameters.Length)
                {
                    var mParam = methodParameters[m + offsetM];

                    if (_specialParameterInjectors.ContainsKey(mParam.ParameterType))
                    {
                        paramsList.Add(_specialParameterInjectors[mParam.ParameterType](context));

                        offsetM++;
                    }
                    else if (m < parameters.Length)
                    {
                        paramsList.Add(ConvertParameter(parameters[m], mParam));

                        m++;
                    }
                    else
                    {
                        paramsList.Add(mParam.DefaultValue);

                        m++;
                    }
                }

                return method.Method.Invoke(method.Target, paramsList.ToArray());
            }

            throw new ArgumentException("Operation not found: " + (controllerName.Equals("$root") ? "(no controller) " : controllerName + "/") + operationName);
        }

        /// <summary>
        /// Calls the server operation
        /// </summary>
        /// <param name="controllerName">the controller name</param>
        /// <param name="operationName">the operation name</param>
        /// <param name="parameters">all parameters expected by the operation, as a dictionary, to match the names</param>
        /// <returns>the return of the operation, not casted</returns>
        public object CallOperation(HttpContext context, string controllerName, string operationName, IDictionary<string, object> parameters)
        {
            controllerName = controllerName ?? "$root";

            if (_operations.ContainsKey(controllerName) && _operations[controllerName].ContainsKey(operationName))
            {
                var method = _operations[controllerName][operationName];
                var methodParameters = method.Method.GetParameters();

                var paramNames = methodParameters.Select(p => p.Name).ToArray();
                var reorderedParams = new List<object>();

                foreach (var mParam in methodParameters)
                {
                    var paramIndex = Array.IndexOf(paramNames, mParam.Name);

                    if (_specialParameterInjectors.ContainsKey(mParam.ParameterType))
                    {
                        reorderedParams.Insert(paramIndex, _specialParameterInjectors[mParam.ParameterType](context));
                    }
                    else if (parameters.ContainsKey(mParam.Name))
                    {
                        reorderedParams.Insert(paramIndex, ConvertParameter(parameters[mParam.Name], mParam));
                    }
                    else
                    {
                        reorderedParams.Add(mParam.DefaultValue);
                    }
                }

                return method.Method.Invoke(method.Target, reorderedParams.ToArray());
            }

            throw new ArgumentException("Operation not found: " + (controllerName.Equals("$root") ? "" : controllerName + "/") + operationName);
        }

        private static object ConvertParameter(object p, ParameterInfo paramInfo)
        {
            var converter = TypeDescriptor.GetConverter(paramInfo.ParameterType);

            if (p == null)
            {
                return null;
            }

            if (converter.CanConvertFrom(p.GetType()))
            {
                return converter.ConvertFrom(p);
            }

            try
            {
                return Convert.ChangeType(p, paramInfo.ParameterType);
            }
            catch
            {
                throw new ArgumentException("Wrong parameter type, expected "
                                            + paramInfo.Name
                                            + " to be "
                                            + paramInfo.ParameterType
                                            + " and it is " + p.GetType());
            }
        }

        /// <summary>
        /// removes a previous registered operation from the server
        /// </summary>
        /// <param name="controllerName">the controller name</param>
        /// <param name="operationName">the operation name</param>
        public void UnbindOperation(string controllerName, string operationName)
        {
            controllerName = controllerName ?? "$root";

            if (_operations.ContainsKey(controllerName) && _operations[controllerName].ContainsKey(operationName))
            {
                _operations[controllerName].Remove(operationName);
            }
        }

        /// <summary>
        /// removes all operations of a previous registered controller and registered operation with the informed
        /// controller name
        /// </summary>
        /// <param name="controllerName">the controller name</param>
        public void UnbindController(string controllerName)
        {
            controllerName = controllerName ?? "$root";

            if (_operations.ContainsKey(controllerName))
            {
                _operations.Remove(controllerName);
            }
        }

        /// <summary>
        /// removes all registered operations and controllers
        /// </summary>
        public void UnbindAllOperations()
        {
            _operations.Clear();
        }

        public void InjectSpecialParameter<T>(Func<HttpContext, T> parameterConstructor)
        {
            _specialParameterInjectors.Add(typeof(T), ctx => parameterConstructor(ctx));
        }

        public void AddRequestInterceptor(Interceptor interc)
        {
            _requestInterceptors.Add(interc);
        }

        public void RemoveRequestInterceptor(Interceptor interc)
        {
            _requestInterceptors.Remove(interc);
        }

        public void AddResponseInterceptor(Interceptor interc)
        {
            _responseInterceptors.Add(interc);
        }

        public void RemoveResponseInterceptor(Interceptor interc)
        {
            _responseInterceptors.Remove(interc);
        }
    }

    internal class OperationTargetAndMethod
    {
        public object Target { get; set; }

        public MethodInfo Method { get; set; }
    }
}
