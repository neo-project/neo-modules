using Neo.Json;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    internal class WebSocketClient : IDisposable, IEquatable<WebSocketClient>
    {
        public WebSocket Socket { get; init; }

        public bool IsConnected =>
            Socket != null &&
            Socket.State == WebSocketState.Open;

        public void Dispose()
        {
            Socket?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task SendJsonAsync(JToken message)
        {
            if (IsConnected)
            {
                await Socket.SendAsync(
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

        public bool Equals(WebSocketClient other) =>
            ReferenceEquals(Socket, other?.Socket);

        public override int GetHashCode() =>
            HashCode.Combine(this, Socket);

        public override bool Equals(object obj)
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
