using Neo.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    internal class WebSocketDictionary<TClient> : IDictionary<Guid, TClient>, IDisposable
        where TClient : WebSocketClient, new()
    {
        private readonly ConcurrentDictionary<Guid, TClient> _clients;

        public WebSocketDictionary()
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
            sendTaskList.AddRange(_clients.Select(s => s.Value.CloseAsync(WebSocketCloseStatus.EndpointUnavailable)));
            await Task.WhenAll(sendTaskList).ConfigureAwait(false);
        }

        public async Task<bool> TryAddAsync(Guid clientId, TClient client)
        {
            if (_clients.TryAdd(clientId, client) == false)
            {
                if (client.IsConnected)
                    await client.CloseAsync(WebSocketCloseStatus.InternalServerError).ConfigureAwait(false);
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
                    await client.SendJsonAsync(message).ConfigureAwait(false);
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

        private async Task SendJsonAndCleanUpAsync(KeyValuePair<Guid, TClient> kvp, JToken message)
        {
            if (kvp.Value.IsConnected)
                await kvp.Value.SendJsonAsync(message).ConfigureAwait(false);
            else
                await TryRemoveAsync(kvp.Key, WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
        }

        #endregion

        #region IDictionary

        public ICollection<Guid> Keys => _clients.Keys;

        public ICollection<TClient> Values => _clients.Values;

        public int Count => _clients.Count;

        public virtual bool IsReadOnly => false;

        public TClient this[Guid key]
        {
            get => _clients[key]; set => _clients[key] = value;
        }

        public virtual void Add(Guid clientId, TClient client) =>
                throw new NotImplementedException();

        public virtual void Add(KeyValuePair<Guid, TClient> client) =>
            throw new NotImplementedException();

        public virtual bool Remove(Guid clientId) =>
            throw new NotImplementedException();

        public bool Remove(KeyValuePair<Guid, TClient> client) =>
            throw new NotImplementedException();

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
