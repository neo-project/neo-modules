// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketClient.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.WsRpcJsonServer
{
    internal class WebSocketClient : IDisposable, IEquatable<WebSocketClient>
    {
        public WebSocket? Socket { get; init; }
        public ConcurrentDictionary<int, WebSocketChannel> EventChannels { get; } = new();

        public bool IsConnected =>
            Socket != null &&
            Socket.State == WebSocketState.Open;

        public virtual void Dispose()
        {
            Socket?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void RemoveChannelRequest(int jsonRpcId) =>
            EventChannels.TryRemove(jsonRpcId, out _);

        public async Task SendJsonAsync(JToken message)
        {
            if (IsConnected)
            {
                await Socket!.SendAsync(
                    new(Encoding.UTF8.GetBytes(message.ToString())),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        public async Task CloseAsync(WebSocketCloseStatus status)
        {
            if (Socket == null)
                return;
            switch (Socket.State)
            {
                case WebSocketState.Connecting:
                case WebSocketState.Open:
                    await Socket.CloseOutputAsync(status, string.Empty, CancellationToken.None).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        #region IEquatable

        public bool Equals(WebSocketClient? other) =>
            ReferenceEquals(Socket, other?.Socket);

        public override int GetHashCode() =>
            HashCode.Combine(this, Socket);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(obj, this))
                return true;
            if (obj == null)
                return false;
            if (obj is not WebSocketClient wsObj)
                return false;
            return Equals(wsObj);
        }

        public static bool operator ==(WebSocketClient left, WebSocketClient right)
        {
            if (left as object is null || right as object is null)
                return Equals(left, right);
            return left.Equals(right);
        }

        public static bool operator !=(WebSocketClient left, WebSocketClient right)
        {
            if (left as object is null || right as object is null)
                return Equals(left, right) == false;
            return left.Equals(right) == false;
        }

        #endregion
    }
}
