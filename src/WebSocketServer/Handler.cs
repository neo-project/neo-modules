using Neo.Json;
namespace Neo.Plugins.WebSocketServer;

public static class Handler
{
    public delegate void WebSocketEventHandler(EventId eventId, WebSocketEvent @event);
}
