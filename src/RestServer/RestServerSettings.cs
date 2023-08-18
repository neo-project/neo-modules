// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.Plugins.RestServer.Newtonsoft.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Net;

namespace Neo.Plugins.RestServer
{
    public class RestServerSettings
    {
        #region Settings

        public uint Network { get; init; }
        public IPAddress BindAddress { get; init; }
        public uint Port { get; init; }
        public uint MaxPageSize { get; init; }
        public long MaxConcurrentConnections { get; init; }
        public uint KeepAliveTimeout { get; init; }
        public bool EnableBasicAuth { get; init; }
        public string RestUser { get; init; }
        public string RestPass { get; init; }
        public bool EnableCors { get; init; }
        public string[] AllowCorsUrls { get; init; }
        public string[] DisableUrlPaths { get; init; }
        public JsonSerializerSettings JsonSerializerSettings { get; init; }

        #endregion

        #region Static Functions

        public static RestServerSettings Default { get; } = new()
        {
            Network = 860833102u,
            BindAddress = IPAddress.None,
            Port = 10339u,
            MaxPageSize = 50u,
            MaxConcurrentConnections = 40L,
            KeepAliveTimeout = 2u,
            EnableBasicAuth = false,
            RestUser = string.Empty,
            RestPass = string.Empty,
            EnableCors = false,
            AllowCorsUrls = Array.Empty<string>(),
            DisableUrlPaths = Array.Empty<string>(),
            JsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                Converters = new JsonConverter[]
                {
                    new StringEnumConverter(),
                    new UInt160JsonConverter(),
                    new UInt256JsonConverter(),
                    new ECPointJsonConverter(),
                    new ReadOnlyMemoryBytesJsonConverter(),
                    new VmArrayJsonConverter(),
                    new VmMapJsonConverter(),
                    new VmStructJsonConverter(),
                    new VmBooleanJsonConverter(),
                    new VmBufferJsonConverter(),
                    new VmByteStringJsonConverter(),
                    new VmIntegerJsonConverter(),
                    new VmNullJsonConverter(),
                    new VmPointerJsonConverter(),
                    new StackItemJsonConverter(),
                },
            },
        };

        public static RestServerSettings Load(IConfigurationSection section) =>
            new()
            {
                Network = section.GetValue(nameof(Network), Default.Network),
                BindAddress = IPAddress.Parse(section.GetSection(nameof(BindAddress)).Value),
                Port = section.GetValue(nameof(Port), Default.Port),
                MaxPageSize = section.GetValue(nameof(MaxPageSize), Default.MaxPageSize),
                MaxConcurrentConnections = section.GetValue(nameof(MaxConcurrentConnections), Default.MaxConcurrentConnections),
                KeepAliveTimeout = section.GetValue(nameof(KeepAliveTimeout), Default.KeepAliveTimeout),
                EnableBasicAuth = section.GetValue(nameof(EnableBasicAuth), Default.EnableBasicAuth),
                RestUser = section.GetValue(nameof(RestUser), Default.RestUser),
                RestPass = section.GetValue(nameof(RestPass), Default.RestPass),
                EnableCors = section.GetValue(nameof(EnableCors), Default.EnableCors),
                AllowCorsUrls = section.GetValue(nameof(AllowCorsUrls), Default.AllowCorsUrls),
                DisableUrlPaths = section.GetValue(nameof(DisableUrlPaths), Default.DisableUrlPaths),
                JsonSerializerSettings = Default.JsonSerializerSettings,
            };

        #endregion

    }
}
