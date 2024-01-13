// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketConnections.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Plugins.Models.WsRpcJsonServer;
using Neo.Plugins.WsRpcJsonServer.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.WsRpcJsonServer
{
    internal class WebSocketConnections<TClient> : IDictionary<Guid, TClient>, IDisposable
        where TClient : WebSocketClient, new()
    {
        private readonly ConcurrentDictionary<Guid, TClient> _clients;

        public static event WebSocketMessageSent? OnMessageSent;
        public static event WebSocketMessageReceived? OnMessageReceived;
        public static event WebSocketConnect? OnConnect;
        public static event WebSocketDisconnect? OnDisconnect;

        public WebSocketConnections()
        {
            _clients = new();
        }

        public void Dispose()
        {
            foreach (var client in _clients.Values)
                client.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Extensions

        public bool IsEmpty => _clients.IsEmpty;

        public async Task DisconnectAllAsync()
        {
            if (_clients.IsEmpty)
                return;

            var sendTaskList = new List<Task>();
            sendTaskList.AddRange(_clients.Select(s =>
            {
                OnDisconnect?.TryCatch(t => t.Invoke(s.Key));
                return s.Value.CloseAsync(WebSocketCloseStatus.EndpointUnavailable);
            }));
            await Task.WhenAll(sendTaskList).ConfigureAwait(false);
        }

        public async Task<bool> TryAddAsync(Guid clientId, TClient client)
        {
            if (_clients.TryAdd(clientId, client) == false)
            {
                if (client.IsConnected)
                {
                    await client.CloseAsync(WebSocketCloseStatus.InternalServerError).ConfigureAwait(false);
                    OnConnect?.TryCatch(t => t.Invoke(clientId));
                }
                return false;
            }
            return true;
        }

        public async Task<bool> TryRemoveAsync(Guid clientId, WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, bool disposeClient = true)
        {
            if (_clients.IsEmpty)
                return true;
            if (_clients.TryRemove(clientId, out var client))
            {
                if (client.IsConnected)
                    await client.CloseAsync(status).ConfigureAwait(false);
                if (disposeClient)
                    client.Dispose();
                OnDisconnect?.TryCatch(t => t.Invoke(clientId));
                return true;
            }
            return false;
        }

        public async Task SendJsonAsync(Guid clientId, JToken message)
        {
            if (_clients.IsEmpty)
                return;
            if (_clients.TryGetValue(clientId, out var client))
            {
                if (client.IsConnected)
                {
                    await client.SendJsonAsync(message).ConfigureAwait(false);
                    OnMessageSent?.TryCatch(t => t.Invoke(clientId, message));
                }
                else
                    await TryRemoveAsync(clientId, WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
            }
        }

        public async Task SendToAllJsonAsync(JToken message)
        {
            if (_clients.IsEmpty)
                return;
            var sendTaskList = new List<Task>();
            sendTaskList.AddRange(_clients.Select(s => SendJsonAndCleanUpAsync(s, message)));
            await Task.WhenAll(sendTaskList).ConfigureAwait(false);
        }

        public async Task ProcessClientAsync(WebSocket client, TaskCompletionSource tcs)
        {
            try
            {
                var clientId = Guid.NewGuid();
                _ = await TryAddAsync(clientId, new TClient { Socket = client });

                while (client.CloseStatus.HasValue == false)
                {
                    var rpcJsonId = -1; // System wide (-1 for system error)
                    try
                    {
                        var message = await ReceiveMessageAsync(clientId, client).ConfigureAwait(false);
                        if (message is not null)
                        {
                            rpcJsonId = message.Id;

                            if (WsRpcJsonServer.Methods.TryGetValue(message.Method!, out var callMethod) == false)
                                throw new WebSocketException(-32601, "Method not found");

                            var obj = callMethod(message.Params!);

                            if (obj is Task<JToken> responseTask)
                                obj = await responseTask.ConfigureAwait(false);

                            obj = RpcJsonResponseMessage.Create(message.Id, (JToken)obj);

                            await SendJsonAsync(clientId, ((RpcJsonResponseMessage)obj).ToJson()).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Indicates server fault or invalid data received
                        ex = ex.InnerException ?? ex;
                        await SendJsonAsync(
                            clientId,
                            RpcJsonResponseMessage.Create(
                                rpcJsonId,
                                WebSocketErrorResult.Create(ex).ToJson())
                            .ToJson());
                    }
                }

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private async Task SendJsonAndCleanUpAsync(KeyValuePair<Guid, TClient> kvp, JToken message)
        {
            if (kvp.Value.IsConnected)
            {
                await kvp.Value.SendJsonAsync(message).ConfigureAwait(false);
                OnMessageSent?.TryCatch(t => t.Invoke(kvp.Key, message));
            }
            else
                await TryRemoveAsync(kvp.Key, WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
        }

        private static async Task<RpcJsonRequestMessage> ReceiveMessageAsync(Guid clientId, WebSocket client)
        {
            var buffer = new byte[WsRpcJsonKestrelSettings.Current?.MessageSize ?? WsRpcJsonKestrelSettings.Default.MessageSize]; // 4096 bytes
            WebSocketReceiveResult? receiveResult = null;

            using var ms = new MemoryStream();
            while (receiveResult == null || receiveResult.EndOfMessage == false)
            {
                receiveResult = await client.ReceiveAsync(new(buffer), CancellationToken.None);
                await ms.WriteAsync(buffer.AsMemory(0, receiveResult.Count)).ConfigureAwait(false);
                Array.Clear(buffer, 0, buffer.Length);
            }

            try
            {
                var json = JToken.Parse(Encoding.UTF8.GetString(ms.ToArray()));

                OnMessageReceived?.TryCatch(t => t.Invoke(clientId, json));

                return RpcJsonRequestMessage.FromJson(json);
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region IDictionary

        public ICollection<Guid> Keys => _clients.Keys;

        public ICollection<TClient> Values => _clients.Values;

        public int Count => _clients.Count;

        public virtual bool IsReadOnly => false;

        public TClient this[Guid key]
        {
            get => _clients[key];
            set => Add(key, value);
        }

        public void Add(Guid clientId, TClient client)
        {
            if (_clients.TryAdd(clientId, client))
                OnConnect?.TryCatch(t => t.Invoke(clientId));
        }

        public void Add(KeyValuePair<Guid, TClient> client)
        {
            if (_clients.TryAdd(client.Key, client.Value))
                OnConnect?.TryCatch(t => t.Invoke(client.Key));
        }

        public bool Remove(Guid clientId)
        {
            if (_clients.TryRemove(clientId, out _))
            {
                OnDisconnect?.TryCatch(t => t.Invoke(clientId));
                return true;
            }
            return false;
        }

        public bool Remove(KeyValuePair<Guid, TClient> client)
        {
            if (_clients.TryRemove(client.Key, out var socket) && ReferenceEquals(client.Value, socket))
            {
                OnDisconnect?.TryCatch(t => t.Invoke(client.Key));
                return true;
            }
            return false;
        }

        public bool TryGetValue(Guid clientId, [MaybeNullWhen(false)] out TClient client) =>
            _clients.TryGetValue(clientId, out client);

        public void Clear() =>
            _clients.Clear();

        public bool ContainsKey(Guid clientId) =>
            _clients.ContainsKey(clientId);

        public bool Contains(KeyValuePair<Guid, TClient> client) =>
            _clients.Contains(client);

        public virtual void CopyTo(KeyValuePair<Guid, TClient>[] array, int arrayIndex) =>
            throw new NotImplementedException();

        public IEnumerator<KeyValuePair<Guid, TClient>> GetEnumerator() =>
            _clients.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();

        #endregion
    }
}
