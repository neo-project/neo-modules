// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcVersion
    {
        public class RpcProtocol
        {
            public uint Network { get; set; }
            public int ValidatorsCount { get; set; }
            public uint MillisecondsPerBlock { get; set; }
            public uint MaxValidUntilBlockIncrement { get; set; }
            public uint MaxTraceableBlocks { get; set; }
            public byte AddressVersion { get; set; }
            public uint MaxTransactionsPerBlock { get; set; }
            public int MemoryPoolMaxTransactions { get; set; }
            public ulong InitialGasDistribution { get; set; }
            public IReadOnlyDictionary<Hardfork, uint> Hardforks { get; set; }

            public JObject ToJson()
            {
                return new JObject
                {
                    ["network"] = Network,
                    ["validatorscount"] = ValidatorsCount,
                    ["msperblock"] = MillisecondsPerBlock,
                    ["maxvaliduntilblockincrement"] = MaxValidUntilBlockIncrement,
                    ["maxtraceableblocks"] = MaxTraceableBlocks,
                    ["addressversion"] = AddressVersion,
                    ["maxtransactionsperblock"] = MaxTransactionsPerBlock,
                    ["memorypoolmaxtransactions"] = MemoryPoolMaxTransactions,
                    ["initialgasdistribution"] = InitialGasDistribution,
                    ["hardforks"] = new JArray(Hardforks.Select(s => new JObject()
                    {
                        // Strip HF_ prefix.
                        ["name"] = StripPrefix(s.Key.ToString(), "HF_"),
                        ["blockheight"] = s.Value,
                    }))
                };
            }

            public static RpcProtocol FromJson(JObject json)
            {
                return new RpcProtocol
                {
                    Network = (uint)json["network"].AsNumber(),
                    ValidatorsCount = (int)json["validatorscount"].AsNumber(),
                    MillisecondsPerBlock = (uint)json["msperblock"].AsNumber(),
                    MaxValidUntilBlockIncrement = (uint)json["maxvaliduntilblockincrement"].AsNumber(),
                    MaxTraceableBlocks = (uint)json["maxtraceableblocks"].AsNumber(),
                    AddressVersion = (byte)json["addressversion"].AsNumber(),
                    MaxTransactionsPerBlock = (uint)json["maxtransactionsperblock"].AsNumber(),
                    MemoryPoolMaxTransactions = (int)json["memorypoolmaxtransactions"].AsNumber(),
                    InitialGasDistribution = (ulong)json["initialgasdistribution"].AsNumber(),
                    Hardforks = new Dictionary<Hardfork, uint>(((JArray)json["hardforks"]).Select(s =>
                    {
                        var name = s["name"].AsString();
                        // Add HF_ prefix to the hardfork response for proper Hardfork enum parsing.
                        return new KeyValuePair<Hardfork, uint>(Enum.Parse<Hardfork>(name.StartsWith("HF_") ? name : $"HF_{name}"), (uint)s["blockheight"].AsNumber());
                    })),
                };
            }

            private static string StripPrefix(string s, string prefix)
            {
                return s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;
            }
        }

        public int TcpPort { get; set; }

        public int WsPort { get; set; }

        public uint Nonce { get; set; }

        public string UserAgent { get; set; }

        public RpcProtocol Protocol { get; set; } = new();

        public JObject ToJson()
        {
            return new JObject
            {
                ["network"] = Protocol.Network, // Obsolete
                ["tcpport"] = TcpPort,
                ["wsport"] = WsPort,
                ["nonce"] = Nonce,
                ["useragent"] = UserAgent,
                ["protocol"] = Protocol.ToJson()
            };
        }

        public static RpcVersion FromJson(JObject json)
        {
            return new RpcVersion
            {
                TcpPort = (int)json["tcpport"].AsNumber(),
                WsPort = (int)json["wsport"].AsNumber(),
                Nonce = (uint)json["nonce"].AsNumber(),
                UserAgent = json["useragent"].AsString(),
                Protocol = RpcProtocol.FromJson((JObject)json["protocol"])
            };
        }
    }
}
