using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Neo;
using Neo.Json;
using Neo.Ledger;
using Neo.Plugins;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public class WebSocketServerPlugin : Plugin
    {
        #region Global Variables

        private readonly WebSocketDictionary<WebSocketClient> _connections;
        private readonly Dictionary<string, Func<JArray, object>> _methods;
        private readonly WebSocketOptions _webSocketOptions;
        private readonly List<NotifyEventArgs> _notifyEvents;

        private NeoSystem _neoSystem;
        private IWebHost _host;

        #endregion

        public override string Name => "WebSocketServer";
        public override string Description => "Enables web socket functionally.";

        public WebSocketServerPlugin()
        {
            _connections = new();
            _methods = new();
            _webSocketOptions = new();
            _notifyEvents = new();
            Blockchain.Committed += OnBlockchainCommitted;
            Blockchain.Committing += OnBlockchainCommitting;
        }

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
            if (system.Settings.Network != WebSocketServerSettings.Current.Network)
                return;

            _neoSystem = system;
            RegisterMethods(this);
            StartWebSocketServer();
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
                await _connections.SendToAllJsonAsync(
                    WebSocketResponseMessage.Create(
                        Guid.Empty,
                        block.Header.ToJson(system.Settings),
                        WebSocketResponseMessageEvent.Block)
                    .ToJson())
                .ConfigureAwait(false));

            foreach (var tx in block.Transactions)
                Task.Run(async () =>
                    await _connections.SendToAllJsonAsync(
                        WebSocketResponseMessage.Create(
                            Guid.Empty,
                            tx.ToJson(system.Settings),
                            WebSocketResponseMessageEvent.Transaction)
                        .ToJson())
                    .ConfigureAwait(false));

            _notifyEvents.ForEach(f =>
                Task.Run(async () =>
                    await _connections.SendToAllJsonAsync(
                        WebSocketResponseMessage.Create(
                            Guid.Empty,
                            f.ToJson(),
                            WebSocketResponseMessageEvent.Notify)
                        .ToJson())
                    .ConfigureAwait(false)));
        }

        public void RegisterMethods(object handler)
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
                    _methods[key] = methodInfo.CreateDelegate<Func<JArray, object>>(handler);
                }
            }
        }

        private void StartWebSocketServer()
        {
            _host = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Listen(WebSocketServerSettings.Current.BindAddress, WebSocketServerSettings.Current.Port);
            })
            //.ConfigureServices(services =>
            //{

            //})
            .Configure(app =>
            {
                app.UseWebSockets(_webSocketOptions);
                app.Run(ProcessRequestsAsync);
            })
            .Build();


            _host.Start();
        }

        private async Task ProcessRequestsAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest == false)
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            else
            {
                var tcs = new TaskCompletionSource();
                using var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

                await ProcessMessagesAsync(ws, tcs).ConfigureAwait(false);

                await tcs.Task;
            }
        }

        public async Task ProcessMessagesAsync(WebSocket client, TaskCompletionSource tcs)
        {
            try
            {
                var clientId = await AddSocketAsync(client).ConfigureAwait(false);

                if (clientId == Guid.Empty)
                    throw new NullReferenceException(nameof(clientId));

                while (client.CloseStatus.HasValue == false)
                {
                    Guid requestId = Guid.Empty;
                    try
                    {
                        var message = await ReceiveMessageAsync(client).ConfigureAwait(false);
                        if (message is not null)
                        {
                            requestId = message.RequestId;

                            if (_methods.TryGetValue(message.Method, out var callMethod) == false)
                                throw new WebSocketException(-32601, "Method not found");

                            var obj = callMethod(message.Params);

                            if (obj is Task<JToken> responseTask)
                                obj = await responseTask.ConfigureAwait(false);

                            obj = WebSocketResponseMessage.Create(message.RequestId, (JToken)obj, WebSocketResponseMessageEvent.Call);

                            await _connections.SendJsonAsync(clientId, ((WebSocketResponseMessage)obj).ToJson()).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex = ex.InnerException ?? ex;
                        await _connections.SendJsonAsync(
                            clientId,
                            WebSocketResponseMessage.Create(
                                requestId,
#if DEBUG
                                WebSocketErrorResponseMessage.Create(100, ex.Message, ex.StackTrace).ToJson(),
#else
                                WebSocketErrorResponseMessage.Create(100, ex.Message).ToJson(),
#endif
                                WebSocketResponseMessageEvent.Error)
                            .ToJson());
                    }
                }

                await _connections
                    .TryRemoveAsync(clientId)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private async Task<Guid> AddSocketAsync(WebSocket socket)
        {
            var clientId = Guid.NewGuid();
            var client = new WebSocketClient()
            {
                Socket = socket
            };

            var result = await _connections.TryAddAsync(clientId, client).ConfigureAwait(false);
            return result ? clientId : Guid.Empty;
        }

        private async Task<WebSocketRequestMessage> ReceiveMessageAsync(WebSocket client)
        {
            var buffer = new byte[WebSocketServerSettings.Current.MessageSize]; // 16384 bytes
            WebSocketReceiveResult receiveResult = null;

            using var ms = new MemoryStream();
            while (receiveResult == null || receiveResult.EndOfMessage == false)
            {
                receiveResult = await client.ReceiveAsync(new(buffer), CancellationToken.None);
                await ms.WriteAsync(buffer.AsMemory(0, receiveResult.Count)).ConfigureAwait(false);
                Array.Clear(buffer, 0, buffer.Length);
            }

            return WebSocketRequestMessage.FromJson(JToken.Parse(Encoding.UTF8.GetString(ms.ToArray())));
        }

        [WebSocketMethod]
        private JToken Echo(JArray @params)
        {
            return @params;
        }
    }
}
