// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketErrorResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketErrorResult
    {
        public int Code { get; init; }
        public string Message { get; init; } = string.Empty;
#if DEBUG
        public string? StackTrace { get; init; }
#endif

        public static WebSocketErrorResult Create(Exception exception) =>
            new()
            {
                Code = exception.HResult,
                Message = exception.Message.Trim(),
#if DEBUG
                StackTrace = exception.StackTrace?.Trim()
#endif
            };

        public static WebSocketErrorResult Create(int code, string message) =>
            new()
            {
                Code = code,
                Message = message.Trim(),
            };

        public override string ToString() =>
            $"{ToJson()}";

        public JToken ToJson() =>
            new JObject()
            {
                ["code"] = Code,
                ["message"] = Message,
#if DEBUG
                ["stackTrace"] = StackTrace,
#endif
            };
    }
}
