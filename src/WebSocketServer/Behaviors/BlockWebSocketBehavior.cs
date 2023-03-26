using System;
using System.Threading.Tasks;
using Neo.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Neo.Plugins.WebSocketServer.Behaviors;

public class BlockWebSocketBehavior : WebSocketBehavior
{
    private Guid ClientId { get; set; }

    public BlockWebSocketBehavior()
    {
        ClientId = Guid.NewGuid();
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        WebSocketServerPlugin.AddClient(ClientId, this);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        WebSocketServerPlugin.RemoveClient(ClientId);
    }

    // public async Task SendPersistedBlockMessage(string message)
    // {
    //     await SendAsync(message, completed => { });
    // }

    protected override void OnMessage(MessageEventArgs e)
    {
        var request = JToken.Parse(e.Data);

        var action = request?["action"]?.ToString();
        if (action == "subscribe")
        {
            // The 'message' parameter will be passed from the WebSocketServerPlugin class
            // Task.Run(() => SendPersistedBlockMessage(message));
        }
    }

    public Task SendPersistedBlockMessage(string message)
    {
        var response = new JObject
        {
            ["action"] = "blockPersisted",
            ["block"] = message
        };
        Send(response.ToString());
        return Task.CompletedTask;
    }
}
