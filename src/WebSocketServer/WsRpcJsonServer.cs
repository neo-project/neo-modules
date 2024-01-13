// Copyright (C) 2015-2024 The Neo Project.
//
// WsRpcJsonServer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Neo.Json;
using Neo.Ledger;
using Neo.Plugins.Models.WsRpcJsonServer;
using Neo.Plugins.WsRpcJsonServer.Models;
using Neo.Plugins.WsRpcJsonServer.V1;
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

namespace Neo.Plugins.WsRpcJsonServer
{
    public delegate void WebSocketConnect(Guid clientId);
    public delegate void WebSocketDisconnect(Guid clientId);
    public delegate void WebSocketRequest(HttpContext httpContext);
    public delegate void WebSocketServerStarted();
    public delegate void WebSocketMessageReceived(Guid clientId, JToken? json);
    public delegate void WebSocketMessageSent(Guid clientId, JToken? json);

    public class WsRpcJsonServer : Plugin
    {
        #region Global Variables

        #region Static

        internal static readonly Dictionary<string, Func<JArray, object>> Methods;
        private static readonly WebSocketConnections<WebSocketClient> _connections;
        public static event WebSocketRequest? OnRequest;
        public static event WebSocketServerStarted? OnServerStarted;

        #endregion

        private readonly WebSocketOptions _webSocketOptions;
        private readonly List<NotifyEventArgs> _notifyEvents;

        private BlockchainMethods? blockchainMethods;
        private NeoSystem? _neoSystem;


        private IWebHost? _host;

        #endregion

        #region Properties

        #region Static

        public static bool HasClients => !_connections.IsEmpty;

        #endregion

        public override string Name => "WebSocketServer";
        public override string Description => "Enables web socket functionally.";

        #endregion


        static WsRpcJsonServer()
        {
            _connections = new();
            Methods = new();
        }

        public WsRpcJsonServer()
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
            if (_neoSystem != null)
                _neoSystem.MemPool.TransactionAdded -= OnMemPoolTransactionAdded!;
            Blockchain.Committing -= OnBlockchainCommitting;
            Blockchain.Committed -= OnBlockchainCommitted;
            GC.SuppressFinalize(this);
        }

        protected override void Configure()
        {
            WsRpcJsonKestrelSettings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != WsRpcJsonKestrelSettings.Current?.Network)
                return;

            _neoSystem = system;

            if (WsRpcJsonKestrelSettings.Current.DebugMode)
            {
                ApplicationEngine.Log += OnApplicationEngineLog!;
                Utility.Logging += OnUtilityLogging;
            }

            _neoSystem.MemPool.TransactionAdded += OnMemPoolTransactionAdded!;
            //_neoSystem.MemPool.TransactionRemoved += OnMemPoolTransactionRemoved;

            blockchainMethods = new BlockchainMethods(system);
            StartWebSocketServer();
        }

        #endregion

        #region Events

        private void OnMemPoolTransactionAdded(object sender, Network.P2P.Payloads.Transaction e)
        {
            if (_connections.IsEmpty)
                return;

            var allClientChannels = _connections.GetAllChannelsWithClients(WebSocketChannelType.MemoryPool);

            _ = Task.Run(async () =>
            {
                foreach (var (clientId, channelRequest) in allClientChannels)
                {
                    var jsonParams = channelRequest.Value.Params;
                    var txHash = WebSocketUtility.TryParseUInt256(jsonParams?.AsString());
                    if (txHash != e.Hash) continue;
                    await _connections.SendJsonAsync(
                        clientId,
                        RpcJsonResponseMessage.Create(
                            channelRequest.Key,
                            e.ToJson(_neoSystem?.Settings))
                        .ToJson()).ConfigureAwait(false);
                    _connections[clientId].RemoveChannelRequest(channelRequest.Key);
                }
            });
        }

        private void OnUtilityLogging(string source, LogLevel level, object message)
        {
            if (_connections.IsEmpty)
                return;

            var allClientChannels = _connections.GetAllChannelsWithClients(WebSocketChannelType.DebugLog);

            _ = Task.Run(async () =>
            {
                foreach (var (clientId, channelRequest) in allClientChannels)
                {
                    var jsonParams = channelRequest.Value.Params;
                    if (jsonParams != null)
                    {
                        var logLevel = Enum.Parse<LogLevel>(jsonParams.AsString());
                        await _connections.SendJsonAsync(
                            clientId,
                            RpcJsonResponseMessage.Create(
                                channelRequest.Key,
                                NeoUtilityLogResult.Create(source, level, message).ToJson())
                            .ToJson())
                        .ConfigureAwait(false);
                    }
                }
            });
        }

        private void OnApplicationEngineLog(object sender, LogEventArgs e)
        {
            if (_connections.IsEmpty)
                return;

            if (e.ScriptContainer?.Hash == null)
                return;

            var allClientChannels = _connections.GetAllChannelsWithClients(WebSocketChannelType.AppLog);

            _ = Task.Run(async () =>
            {
                foreach (var (clientId, channelRequest) in allClientChannels)
                {
                    var jsonParams = channelRequest.Value.Params;
                    if (jsonParams != null)
                    {
                        var contractHash = WebSocketUtility.TryParseScriptHash(jsonParams.AsString(), _neoSystem!.Settings.AddressVersion);
                        if (contractHash == e.ScriptHash)
                        {
                            await _connections.SendToAllJsonAsync(
                                RpcJsonResponseMessage.Create(
                                    channelRequest.Key,
                                    e.ToJson())
                                .ToJson())
                            .ConfigureAwait(false);
                        }
                    }
                }
            });
        }

        private void OnBlockchainCommitting(
            NeoSystem system,
            Network.P2P.Payloads.Block block,
            Persistence.DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (_connections.IsEmpty)
                return;

            _notifyEvents.Clear();

            foreach (var appExec in applicationExecutedList.Where(w => w.Transaction != null))
                _notifyEvents.AddRange(appExec.Notifications);
        }

        private void OnBlockchainCommitted(NeoSystem system, Network.P2P.Payloads.Block block)
        {
            if (_connections.IsEmpty)
                return;

            _connections.AsParallel().ForAll(async conn =>
            {
                var allClientChannels = conn.Value.EventChannels
                    .Where(w =>
                        w.Value.ChannelType == WebSocketChannelType.Block ||
                        w.Value.ChannelType == WebSocketChannelType.Transaction ||
                        w.Value.ChannelType == WebSocketChannelType.ContractNotify);

                foreach (var channelRequest in allClientChannels)
                {
                    var jsonParams = channelRequest.Value.Params;
                    if (jsonParams != null)
                    {
                        switch (channelRequest.Value.ChannelType)
                        {
                            case WebSocketChannelType.Block:
                                {
                                    await _connections.SendJsonAsync(
                                            conn.Key,
                                            RpcJsonResponseMessage.Create(
                                                channelRequest.Key,
                                                block.Header.ToJson(system.Settings))
                                            .ToJson()).ConfigureAwait(false);
                                    break;
                                }
                            case WebSocketChannelType.Transaction:
                                {
                                    var txHash = WebSocketUtility.TryParseUInt256(jsonParams?.AsString());
                                    var tx = block.Transactions.SingleOrDefault(s => s.Hash == txHash);
                                    if (tx != null)
                                    {
                                        await _connections.SendJsonAsync(
                                            conn.Key,
                                            RpcJsonResponseMessage.Create(
                                                channelRequest.Key,
                                                tx.ToJson(system.Settings))
                                            .ToJson()).ConfigureAwait(false);
                                        _connections[conn.Key].RemoveChannelRequest(channelRequest.Key);
                                    }
                                    break;
                                }
                            case WebSocketChannelType.ContractNotify:
                                {
                                    var contractHash = WebSocketUtility.TryParseScriptHash(jsonParams?.AsString(), system.Settings.AddressVersion);
                                    var contractEvents = _notifyEvents.Where(w => w.ScriptHash == contractHash);
                                    foreach (var contractEvent in contractEvents)
                                    {
                                        await _connections.SendJsonAsync(
                                            conn.Key,
                                            RpcJsonResponseMessage.Create(
                                                channelRequest.Key,
                                                contractEvent.ToJson())
                                            .ToJson()).ConfigureAwait(false);
                                    }
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }
            });
        }

        #endregion

        #region Public Methods

        public static void RegisterMethods(object handler)
        {
            MethodInfo[] array = handler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo methodInfo in array)
            {
                WsRpcJsonMethodAttribute customAttribute = methodInfo.GetCustomAttribute<WsRpcJsonMethodAttribute>()!;
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

        public static void SendJson(Guid clientId, int id, JToken json)
        {
            if (_connections.IsEmpty)
                return;

            _ = Task.Run(async () =>
                await _connections.SendJsonAsync(
                    clientId,
                    RpcJsonResponseMessage.Create(
                        id,
                        json)
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
                options.Listen(WsRpcJsonKestrelSettings.Current?.BindAddress ?? WsRpcJsonKestrelSettings.Default.BindAddress, WsRpcJsonKestrelSettings.Current?.Port ?? WsRpcJsonKestrelSettings.Default.Port, config =>
                {
                    if (string.IsNullOrEmpty(WsRpcJsonKestrelSettings.Current?.SslCertFile))
                        return;
                    config.UseHttps(WsRpcJsonKestrelSettings.Current.SslCertFile, WsRpcJsonKestrelSettings.Current.SslCertPassword, httpsConnectionAdapterOptions =>
                    {
                        if (WsRpcJsonKestrelSettings.Current.TrustedAuthorities is null || WsRpcJsonKestrelSettings.Current.TrustedAuthorities.Length == 0)
                            return;
                        httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        httpsConnectionAdapterOptions.ClientCertificateValidation = (cert, chain, err) =>
                        {
                            if (err != SslPolicyErrors.None)
                                return false;
                            X509Certificate2 authority = chain!.ChainElements[^1].Certificate;
                            return WsRpcJsonKestrelSettings.Current.TrustedAuthorities.Contains(authority!.Thumbprint);
                        };
                    });
                });
            })
            //.ConfigureServices(services =>
            //{

            //})
            .Configure(app =>
            {
                _webSocketOptions.KeepAliveInterval = TimeSpan.FromSeconds(WsRpcJsonKestrelSettings.Current?.ConcurrentProxyTimeout ?? WsRpcJsonKestrelSettings.Default.ConcurrentProxyTimeout);

                foreach (var origin in WsRpcJsonKestrelSettings.Current?.AllowOrigins ?? WsRpcJsonKestrelSettings.Default.AllowOrigins)
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
            if (WsRpcJsonKestrelSettings.Current?.EnableBasicAuthentication ?? WsRpcJsonKestrelSettings.Default.EnableBasicAuthentication)
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
                if (authValue.Scheme.Equals("basic", StringComparison.InvariantCultureIgnoreCase) && authValue.Parameter != null)
                {
                    try
                    {
                        var decodedParams = Encoding.UTF8.GetString(Convert.FromBase64String(authValue.Parameter));
                        var creds = decodedParams.Split(':', 2);
                        if (creds[0] == WsRpcJsonKestrelSettings.Current?.User && creds[1] == WsRpcJsonKestrelSettings.Current?.Pass)
                            return true;

                    }
                    catch
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
