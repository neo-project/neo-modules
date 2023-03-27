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
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcInvokeResult
    {
        public string Script { get; set; }

        public VM.VMState State { get; set; }

        public long GasConsumed { get; set; }

        public StackItem[] Stack { get; set; }

        public string Tx { get; set; }

        public string Exception { get; set; }

        public string Session { get; set; }

        public JObject ToJson()
        {
            JObject json = new()
            {
                ["script"] = Script,
                ["state"] = State,
                ["gasconsumed"] = GasConsumed.ToString()
            };
            if (!string.IsNullOrEmpty(Exception))
                json["exception"] = Exception;
            try
            {
                json["stack"] = new JArray(Stack.Select(p => p.ToJson()));
            }
            catch (InvalidOperationException)
            {
                // ContractParameter.ToJson() may cause InvalidOperationException
                json["stack"] = "error: recursive reference";
            }
            if (!string.IsNullOrEmpty(Tx)) json["tx"] = Tx;
            return json;
        }

        public static RpcInvokeResult FromJson(JObject json)
        {
            RpcInvokeResult invokeScriptResult = new()
            {
                Script = json["script"].AsString(),
                State = json["state"].GetEnum<VMState>(),
                GasConsumed = long.Parse(json["gasconsumed"].AsString()),
            };
            invokeScriptResult.Exception = json["exception"]?.AsString();
            invokeScriptResult.Session = json["session"]?.AsString();
            try
            {
                invokeScriptResult.Stack = ((JArray)json["stack"]).Select(p => Utility.StackItemFromJson((JObject)p)).ToArray();
            }
            catch { }
            invokeScriptResult.Tx = json["tx"]?.AsString();
            return invokeScriptResult;
        }
    }

    public class RpcStack
    {
        public string Type { get; set; }

        public string Value { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["type"] = Type;
            json["value"] = Value;
            return json;
        }

        public static RpcStack FromJson(JObject json)
        {
            return new RpcStack
            {
                Type = json["type"].AsString(),
                Value = json["value"].AsString()
            };
        }
    }
}
