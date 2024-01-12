// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketUtilityLogResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.Plugins
{
    internal class WebSocketUtilityLogResult
    {
        public string? SourceName { get; init; }
        public LogLevel LogLevel { get; init; }
        public object? Message { get; init; }

        public static WebSocketUtilityLogResult Create(string sourceName, LogLevel level, object message) =>
            new()
            {
                SourceName = sourceName,
                LogLevel = level,
                Message = message
            };

        public override string ToString() =>
            $"{ToJson()}";

        public JToken ToJson() =>
            new JObject()
            {
                ["source"] = SourceName,
                ["level"] = $"{LogLevel}",
                ["message"] = $"{Message}",
            };
    }
}
