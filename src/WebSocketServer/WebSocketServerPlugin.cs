using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo.ConsoleService;
using Newtonsoft.Json.Linq;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.WebSocketServer.Behaviors;

namespace Neo.Plugins.WebSocketServer;

public class WebSocketServerPlugin : Plugin
{
    public override string Name => "NeoWebSocketServer";
    public override string Description => "Enables WebSocket notifications for the node";

    private Settings _settings;
    private static readonly Dictionary<Guid, BlockWebSocketBehavior> Handlers = new();
    private static WebSocketSharp.Server.WebSocketServer _server;
    private NeoSystem _system;

    public WebSocketServerPlugin()
    {
        Blockchain.Committed += OnCommitted;
    }

    protected override void Configure()
    {
        _settings ??= new Settings(GetConfiguration());
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        _system = system;
    }

    public override void Dispose()
    {
        Blockchain.Committed -= OnCommitted;
        _server?.Stop();
        base.Dispose();
    }

    [ConsoleCommand("start wss", Category = "wss", Description = "Open Web Socket Server")]
    private void OnStart()
    {
        if (_server is { IsListening: true }) return;

        var s = _settings.Servers.FirstOrDefault(p => p.Network == _system.Settings.Network);
        if (s == null) return;

        var useSsl = !string.IsNullOrEmpty(s.SslCert) && !string.IsNullOrEmpty(s.SslCertPassword);
        _server = new WebSocketSharp.Server.WebSocketServer(s.BindAddress, s.Port, useSsl);
        if (useSsl)
        {
            _server.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(s.SslCert, s.SslCertPassword);
        }
        _server.AddWebSocketService<BlockWebSocketBehavior>("/block", () => new BlockWebSocketBehavior());
        _server.Start();
    }

    [ConsoleCommand("close wss", Category = "wss", Description = "Close Web Socket Server")]
    private void OnClose()
    {
        if (_server is { IsListening: true }) _server.Stop();
        ConsoleHelper.Info("Web Socket Server closed");
    }

    private static async void OnCommitted(NeoSystem system, Block block)
    {
        using var snapshot = system.GetSnapshot();
        var blockJson = JObject.FromObject(block);
        await SendMessageToClients(blockJson.ToString());
    }

    private static async Task SendMessageToClients(string message)
    {
        var sendTasks = new List<Task>();

        lock (Handlers)
        {
            sendTasks.AddRange(Handlers.Values.Select(handler => handler.SendPersistedBlockMessage(message)));
        }

        await Task.WhenAll(sendTasks);
    }

    public static void AddClient(Guid clientId, BlockWebSocketBehavior client)
    {
        lock (Handlers)
        {
            Handlers[clientId] = client;
        }
    }

    public static void RemoveClient(Guid clientId)
    {
        lock (Handlers)
        {
            Handlers.Remove(clientId);
        }
    }
}
