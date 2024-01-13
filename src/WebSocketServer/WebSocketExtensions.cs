// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.WsRpcJsonServer
{
    public static class WebSocketExtensions
    {
        public static JToken ToJson(this NotifyEventArgs args, bool showTxHash = true)
        {
            var json = new JObject()
            {
                ["scripthash"] = $"{args?.ScriptHash}",
                ["eventname"] = $"{args?.EventName}",
                ["state"] = args?.State?.Count > 0 ?
                    new JArray(args!.State.Select(s => s.ToJson())) :
                    Array.Empty<JToken?>(),
            };

            if (showTxHash)
                json["txhash"] = $"{args?.ScriptContainer?.Hash}";

            return json;
        }

        public static JToken ToJson(this LogEventArgs logEventArgs) =>
            new JObject()
            {
                ["txhash"] = $"{logEventArgs?.ScriptContainer?.Hash}",
                ["contract"] = $"{logEventArgs?.ScriptHash}",
                ["message"] = logEventArgs?.Message,
            };

        public static void TryCatch<TSource>(this TSource eventObject, Action<TSource> action)
        {
            try
            {
                action(eventObject);
            }
            catch
            {
            }
        }

        internal static IEnumerable<(Guid, KeyValuePair<int, WebSocketChannel>)> GetAllChannelsWithClients<TClient>(
            this WebSocketConnections<TClient> connections,
            WebSocketChannelType channelType) where TClient : WebSocketClient, new() =>
            connections.Keys
                .Zip(connections.Values
                    .SelectMany(s => s.EventChannels)
                    .Where(w => w.Value.ChannelType == channelType));
    }
}
