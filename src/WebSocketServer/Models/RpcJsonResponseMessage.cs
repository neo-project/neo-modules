// Copyright (C) 2015-2024 The Neo Project.
//
// RpcJsonResponseMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;
using System.Text;

namespace Neo.Plugins.Models.WsRpcJsonServer
{
    internal class RpcJsonResponseMessage : IEquatable<RpcJsonResponseMessage>
    {
        public Version Version { get; private init; } = new("2.0");
        public int Id { get; private init; }
        public JToken? Result { get; private init; }

        internal static RpcJsonResponseMessage Create(int id, JToken result) =>
            new()
            {
                Id = id,
                Result = result,
            };

        public JToken ToJson() =>
            new JObject()
            {
                ["jsonrpc"] = $"{Version}",
                ["id"] = Id,
                ["result"] = Result,
            };

        public static RpcJsonResponseMessage FromJson(JToken message) =>
            new()
            {
                Version = message["jsonrpc"] == null ? new("1.0") : new(message["jsonrpc"]!.AsString()),
                Id = message["id"] == null ? 0 : unchecked((int)message["id"]!.AsNumber()),
                Result = message["result"],
            };

        public override string ToString() =>
            $"{ToJson()}";

        public byte[] ToArray() =>
            Encoding.UTF8.GetBytes(ToString());

        public bool Equals(RpcJsonResponseMessage? other) =>
            other != null && other.Id == Id &&
            other.Version == Version && other.Result == Result;

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as RpcJsonResponseMessage);
        }

        public override int GetHashCode() =>
            HashCode.Combine(this, Version, Id, Result);
    }
}
