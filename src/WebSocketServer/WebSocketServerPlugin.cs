using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Neo.Json;
using Neo.Ledger;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public delegate void WebSocketConnect(Guid clientId);
    public delegate void WebSocketDisconnect(Guid clientId);
    public delegate void WebSocketRequest(HttpContext httpContext);
    public delegate void WebSocketServerStarted();
    public delegate void WebSocketMessageReceived(Guid clientId, JToken json);
    public delegate void WebSocketMessageSent(Guid clientId, JToken json);

    public class WebSocketServerPlugin : Plugin
    {
        #region Global Variables

        #region Static

        internal static readonly Dictionary<string, Func<JArray, object>> Methods;
        private static readonly WebSocketConnection<WebSocketClient> _connections;
        public static event WebSocketRequest OnRequest;
        public static event WebSocketServerStarted OnServerStarted;

        #endregion

        private readonly WebSocketOptions _webSocketOptions;
        private readonly List<NotifyEventArgs> _notifyEvents;


        private IWebHost _host;

        #endregion

        #region Properties

        #region Static

        public static bool HasClients => !_connections.IsEmpty;

        #endregion

        public override string Name => "WebSocketServer";
        public override string Description => "Enables web socket functionally.";

        #endregion


        static WebSocketServerPlugin()
        {
            _connections = new();
            Methods = new();
        }

        public WebSocketServerPlugin()
        {
            _webSocketOptions = new();
            _notifyEvents = new();
            Blockchain.Committed += OnBlockchainCommitted;
            Blockchain.Committing += OnBlockchainCommitting;
        }

        #region Overrides

        public override void Dispose()
        {
            _host?.Dispose();
            _connections?.Dispose();
            Blockchain.Committed -= OnBlockchainCommitted;
            GC.SuppressFinalize(this);
        }

        protected override void Configure()
        {
            WebSocketServerSettings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != WebSocketServerSettings.Current?.Network)
                return;

            if (WebSocketServerSettings.Current.DebugMode)
            {
                ApplicationEngine.Log += OnApplicationEngineLog;
                Utility.Logging += OnUtilityLogging;
            }

            //RegisterMethods(this);
            StartWebSocketServer();
        }

        #endregion

        #region Events

        private void OnUtilityLogging(string source, LogLevel level, object message)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendAllJsonAsync(
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        WebSocketUtilityLogResult.Create(source, level, message).ToJson(),
                        WebSocketResponseMessageEvent.System)
                    .ToJson())
                .ConfigureAwait(false));
        }

        private void OnApplicationEngineLog(object sender, LogEventArgs e)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendAllJsonAsync(
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        e.ToJson(),
                        WebSocketResponseMessageEvent.Log)
                    .ToJson())
                .ConfigureAwait(false));
        }

        private void OnBlockchainCommitting(
            NeoSystem system,
            Neo.Network.P2P.Payloads.Block block,
            Neo.Persistence.DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (_connections.IsEmpty)
                return;

            _notifyEvents.Clear();

            foreach (var appExec in applicationExecutedList.Where(w => w.Transaction != null))
                _notifyEvents.AddRange(appExec.Notifications);
        }

        private void OnBlockchainCommitted(NeoSystem system, Neo.Network.P2P.Payloads.Block block)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendAllJsonAsync(
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        block.Header.ToJson(system.Settings),
                        WebSocketResponseMessageEvent.Block)
                    .ToJson())
                .ConfigureAwait(false));

            foreach (var tx in block.Transactions)
                Task.Run(async () =>
                    await _connections.SendAllJsonAsync(
                        WebSocketResponseMessage.Create(
                            Guid.Empty,
                            tx.ToJson(system.Settings),
                            WebSocketResponseMessageEvent.Transaction)
                        .ToJson())
                    .ConfigureAwait(false));

            _notifyEvents.ForEach(f =>
                Task.Run(async () =>
                    await _connections.SendAllJsonAsync(
                        WebSocketResponseMessage.Create(
                            Guid.Empty,
                            f.ToJson(),
                            WebSocketResponseMessageEvent.Notify)
                        .ToJson())
                    .ConfigureAwait(false)));
        }

        #endregion

        #region Public Methods

        public static void RegisterMethods(object handler)
        {
            MethodInfo[] array = handler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo methodInfo in array)
            {
                WebSocketMethodAttribute customAttribute = methodInfo.GetCustomAttribute<WebSocketMethodAttribute>()!;
                if (customAttribute != null)
                {
                    string key = string.IsNullOrEmpty(customAttribute.Name) ?
                        methodInfo.Name.ToLowerInvariant() :
                        customAttribute.Name.ToLowerInvariant();
                    Methods[key] = methodInfo.CreateDelegate<Func<JArray, object>>(handler);
                }
            }
        }

        #region Static

        public static void SendAllJson(JToken json)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendAllJsonAsync(
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        json,
                        WebSocketResponseMessageEvent.System)
                    .ToJson())
                .ConfigureAwait(false));
        }

        public static void SendJson(Guid clientId, byte eventId, JToken json)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendJsonAsync(
                    clientId,
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        json,
                        eventId)
                    .ToJson())
                .ConfigureAwait(false));
        }

        public static void SendJson(Guid clientId, WebSocketResponseMessageEvent eventId, JToken json)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendJsonAsync(
                    clientId,
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        json,
                        eventId)
                    .ToJson())
                .ConfigureAwait(false));
        }

        #endregion


        #endregion

        #region Private Methods

        private void StartWebSocketServer()
        {
            _host = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Listen(WebSocketServerSettings.Current.BindAddress, WebSocketServerSettings.Current.Port, config =>
                {
                    if (string.IsNullOrEmpty(WebSocketServerSettings.Current.SslCertFile))
                        return;
                    config.UseHttps(WebSocketServerSettings.Current.SslCertFile, WebSocketServerSettings.Current.SslCertPassword, httpsConnectionAdapterOptions =>
                    {
                        if (WebSocketServerSettings.Current.TrustedAuthorities is null || WebSocketServerSettings.Current.TrustedAuthorities.Length == 0)
                            return;
                        httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                        {
                            if (err != SslPolicyErrors.None)
                                return false;
                            X509Certificate2 authority = chain.ChainElements[^1].Certificate;
                            return WebSocketServerSettings.Current.TrustedAuthorities.Contains(authority.Thumbprint);
                        };
                    });
                });
            })
            //.ConfigureServices(services =>
            //{

            //})
            .Configure(app =>
            {
                _webSocketOptions.KeepAliveInterval = TimeSpan.FromSeconds(WebSocketServerSettings.Current.ConcurrentProxyTimeout);

                foreach (var origin in WebSocketServerSettings.Current.AllowOrigins)
                    _webSocketOptions.AllowedOrigins.Add(origin);

                app.UseWebSockets(_webSocketOptions);
                app.Run(ProcessRequestsAsync);
            })
            .Build();


            _host.Start();
            OnServerStarted?.TryCatch(t => t.Invoke());
        }

        private async Task ProcessRequestsAsync(HttpContext context)
        {
            if (WebSocketServerSettings.Current.EnableBasicAuthentication)
            {
                if (IsAuthorized(context) == false)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }


            if (context.WebSockets.IsWebSocketRequest == false)
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            else
            {
                var tcs = new TaskCompletionSource();
                using var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

                OnRequest?.TryCatch(t => t.Invoke(context));

                await _connections.ProcessClientAsync(ws, tcs).ConfigureAwait(false);

                await tcs.Task;
            }
        }

        #region Static

        private static bool IsAuthorized(HttpContext context)
        {
            var authHeader = context.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authHeader) == false && AuthenticationHeaderValue.TryParse(authHeader, out var authValue))
            {
                if (authValue.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase) && authValue.Parameter != null)
                {
                    try
                    {
                        var decodedParams = Encoding.UTF8.GetString(Convert.FromBase64String(authValue.Parameter));
                        var creds = decodedParams.Split(':', 2);
                        if (creds[0] == WebSocketServerSettings.Current.User && creds[1] == WebSocketServerSettings.Current.Pass)
                            return true;

                    }
                    catch (FormatException)
                    {
                    }
                }
            }
            return false;
        }

        #endregion

        #endregion

    }
}
