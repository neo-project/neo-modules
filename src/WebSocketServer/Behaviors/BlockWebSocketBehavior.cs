using System.Threading.Tasks;
using Neo.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Neo.Plugins.WebSocketServer.Behaviors;

class BlockWebSocketBehavior : WebSocketBehavior
{
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
