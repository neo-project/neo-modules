// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Neo.Plugins
{
    class Settings
    {
        public IReadOnlyList<RpcServerSettings> Servers { get; init; }

        public Settings(IConfigurationSection section)
        {
            Servers = section.GetSection(nameof(Servers)).GetChildren().Select(p => RpcServerSettings.Load(p)).ToArray();
        }
    }

    public record RpcServerSettings
    {
        public uint Network { get; init; }
        public IPAddress BindAddress { get; init; }
        public ushort Port { get; init; }
        public string SslCert { get; init; }
        public string SslCertPassword { get; init; }
        public string[] TrustedAuthorities { get; init; }
        public int MaxConcurrentConnections { get; init; }
        public int MaxRequestBodySize { get; init; }
        public string RpcUser { get; init; }
        public string RpcPass { get; init; }
        public long MaxGasInvoke { get; init; }
        public long MaxFee { get; init; }
        public int MaxIteratorResultItems { get; init; }
        public int MaxStackSize { get; init; }
        public string[] DisabledMethods { get; init; }
        public bool SessionEnabled { get; init; }
        public TimeSpan SessionExpirationTime { get; init; }
        public int FindStoragePageSize { get; init; }

        public static RpcServerSettings Default { get; } = new RpcServerSettings
        {
            Network = 5195086u,
            BindAddress = IPAddress.None,
            SslCert = string.Empty,
            SslCertPassword = string.Empty,
            MaxGasInvoke = (long)new BigDecimal(10M, NativeContract.GAS.Decimals).Value,
            MaxFee = (long)new BigDecimal(0.1M, NativeContract.GAS.Decimals).Value,
            TrustedAuthorities = Array.Empty<string>(),
            MaxIteratorResultItems = 100,
            MaxStackSize = ushort.MaxValue,
            DisabledMethods = Array.Empty<string>(),
            MaxConcurrentConnections = 40,
            MaxRequestBodySize = 5 * 1024 * 1024,
            SessionEnabled = false,
            SessionExpirationTime = TimeSpan.FromSeconds(60),
            FindStoragePageSize = 50
        };

        public static RpcServerSettings Load(IConfigurationSection section) => new()
        {
            Network = section.GetValue("Network", Default.Network),
            BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value),
            Port = ushort.Parse(section.GetSection("Port").Value),
            SslCert = section.GetSection("SslCert").Value,
            SslCertPassword = section.GetSection("SslCertPassword").Value,
            TrustedAuthorities = section.GetSection("TrustedAuthorities").GetChildren().Select(p => p.Get<string>()).ToArray(),
            RpcUser = section.GetSection("RpcUser").Value,
            RpcPass = section.GetSection("RpcPass").Value,
            MaxGasInvoke = (long)new BigDecimal(section.GetValue<decimal>("MaxGasInvoke", Default.MaxGasInvoke), NativeContract.GAS.Decimals).Value,
            MaxFee = (long)new BigDecimal(section.GetValue<decimal>("MaxFee", Default.MaxFee), NativeContract.GAS.Decimals).Value,
            MaxIteratorResultItems = section.GetValue("MaxIteratorResultItems", Default.MaxIteratorResultItems),
            MaxStackSize = section.GetValue("MaxStackSize", Default.MaxStackSize),
            DisabledMethods = section.GetSection("DisabledMethods").GetChildren().Select(p => p.Get<string>()).ToArray(),
            MaxConcurrentConnections = section.GetValue("MaxConcurrentConnections", Default.MaxConcurrentConnections),
            MaxRequestBodySize = section.GetValue("MaxRequestBodySize", Default.MaxRequestBodySize),
            SessionEnabled = section.GetValue("SessionEnabled", Default.SessionEnabled),
            SessionExpirationTime = TimeSpan.FromSeconds(section.GetValue("SessionExpirationTime", (int)Default.SessionExpirationTime.TotalSeconds)),
            FindStoragePageSize = section.GetValue("FindStoragePageSize", Default.FindStoragePageSize)
        };
    }
}
