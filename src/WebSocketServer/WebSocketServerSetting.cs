using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.WebSocketServer;

public record WebSocketServerSetting
{
    public uint Network { get; init; }
    public IPAddress BindAddress { get; init; }
    public ushort Port { get; init; }
    public string SslCert { get; init; }
    public string SslCertPassword { get; init; }
    public List<string> TrustedAuthorities { get; init; }
    public int MaxConcurrentConnections { get; init; }
    public bool SessionEnabled { get; init; }
    public TimeSpan SessionExpirationTime { get; init; }

    public static WebSocketServerSetting Default { get; } = new()
    {
        Network = 5195086u,
        BindAddress = IPAddress.None,
        MaxConcurrentConnections = 40,
        SessionEnabled = false,
        SessionExpirationTime = TimeSpan.FromSeconds(60)
    };

    public static WebSocketServerSetting Load(IConfigurationSection section) => new()
    {
        Network = section.GetValue("Network", Default.Network),
        BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value),
        Port = ushort.Parse(section.GetSection("Port").Value),
        SslCert = section.GetValue("SslCert", ""),
        SslCertPassword = section.GetValue("SslCertPassword", ""),
        TrustedAuthorities = section.GetSection("TrustedAuthorities").Get<List<string>>() ?? new List<string>(),
        MaxConcurrentConnections = section.GetValue("MaxConcurrentConnections", Default.MaxConcurrentConnections),
        SessionEnabled = section.GetValue("SessionEnabled", Default.SessionEnabled),
        SessionExpirationTime = TimeSpan.FromSeconds(section.GetValue("SessionExpirationTime", (int)Default.SessionExpirationTime.TotalSeconds))
    };
}

public class Settings
{
    public IReadOnlyList<WebSocketServerSetting> Servers { get; init; }
    public static int MaxStackSize { get; private set; }

    public Settings(IConfigurationSection section)
    {
        Servers = section.GetSection(nameof(Servers)).GetChildren().Select(p => WebSocketServerSetting.Load(p)).ToArray();
        MaxStackSize = section.GetValue("MaxStackSize", (int)ushort.MaxValue);
    }
}
