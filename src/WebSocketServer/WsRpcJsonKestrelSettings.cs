// Copyright (C) 2015-2024 The Neo Project.
//
// WsRpcJsonKestrelSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System;
using System.Net;

namespace Neo.Plugins.WsRpcJsonServer
{
    public class WsRpcJsonKestrelSettings
    {
        public uint Network { get; private init; }
        public IPAddress BindAddress { get; private set; } = IPAddress.Loopback;
        public int Port { get; private init; }
        public string? SslCertFile { get; private init; }
        public string? SslCertPassword { get; private init; }
        public string[] TrustedAuthorities { get; private init; } = Array.Empty<string>();
        public bool EnableBasicAuthentication { get; private init; }
        public string? User { get; private init; }
        public string? Pass { get; private init; }
        public string[] AllowOrigins { get; private init; } = Array.Empty<string>();
        public uint ConcurrentProxyTimeout { get; private init; }
        public uint MessageSize { get; private init; }
        public long MaxGasInvoke { get; private init; }
        public uint WalletSessionTimeout { get; private init; }
        public bool DebugMode { get; private init; }

        public static WsRpcJsonKestrelSettings Default => new()
        {
            Network = 5195086u,
            BindAddress = IPAddress.Loopback,
            Port = 10340,
            SslCertFile = string.Empty,
            SslCertPassword = string.Empty,
            TrustedAuthorities = Array.Empty<string>(),
            EnableBasicAuthentication = false,
            User = string.Empty,
            Pass = string.Empty,
            AllowOrigins = Array.Empty<string>(),
            ConcurrentProxyTimeout = 120u,
            MessageSize = 1024u * 4u,
            MaxGasInvoke = 20_00000000L,
            WalletSessionTimeout = 120u,
            DebugMode = false,
        };

        public static WsRpcJsonKestrelSettings? Current { get; private set; }

        internal static void Load(IConfigurationSection section)
        {
            Current = new()
            {
                Network = section.GetValue(nameof(Network), Default.Network),
                Port = section.GetValue(nameof(Port), Default.Port),
                SslCertFile = section.GetValue(nameof(SslCertFile), Default.SslCertFile),
                SslCertPassword = section.GetValue(nameof(SslCertPassword), Default.SslCertPassword),
                TrustedAuthorities = section.GetSection(nameof(TrustedAuthorities))?.Get<string[]>() ?? Default.TrustedAuthorities,
                EnableBasicAuthentication = section.GetValue(nameof(EnableBasicAuthentication), Default.EnableBasicAuthentication),
                User = section.GetValue(nameof(User), Default.User),
                Pass = section.GetValue(nameof(Pass), Default.Pass),
                AllowOrigins = section.GetSection(nameof(AllowOrigins))?.Get<string[]>() ?? Default.AllowOrigins,
                ConcurrentProxyTimeout = section.GetValue(nameof(ConcurrentProxyTimeout), Default.ConcurrentProxyTimeout),
                MessageSize = section.GetValue(nameof(MessageSize), Default.MessageSize),
                MaxGasInvoke = section.GetValue(nameof(MaxGasInvoke), Default.MaxGasInvoke),
                WalletSessionTimeout = section.GetValue(nameof(WalletSessionTimeout), Default.WalletSessionTimeout),
                DebugMode = section.GetValue(nameof(DebugMode), Default.DebugMode),
            };

            if (IPAddress.TryParse(section.GetSection(nameof(BindAddress)).Value, out var ipAddress))
                Current.BindAddress = ipAddress;
            else
                Current.BindAddress = Default.BindAddress;
        }
    }
}
