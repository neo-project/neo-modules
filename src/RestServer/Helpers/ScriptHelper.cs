// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.SmartContract;
using Neo.VM.Types;
using Neo.VM;
using Neo.SmartContract.Manifest;
using Array = System.Array;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Plugins.RestServer.Helpers
{
    internal static class ScriptHelper
    {
        public static bool InvokeMethod(ProtocolSettings protocolSettings, RestServerSettings restSettings, DataCache snapshot, UInt160 scriptHash, string method, out StackItem[] results, params object[] args)
        {
            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitDynamicCall(scriptHash, method, CallFlags.ReadOnly, args);
            byte[] script = scriptBuilder.ToArray();
            using var engine = ApplicationEngine.Run(script, snapshot, settings: protocolSettings, gas: restSettings.MaxInvokeGas);
            results = engine.State == VMState.FAULT ? Array.Empty<StackItem>() : engine.ResultStack.ToArray();
            return engine.State == VMState.HALT;
        }

        public static ApplicationEngine InvokeMethod(ProtocolSettings protocolSettings, RestServerSettings restSettings, DataCache snapshot, UInt160 scriptHash, string method, JToken args)
        {
            var aparams = ((JArray)args).Select(FromJson).ToArray();
            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitDynamicCall(scriptHash, method, CallFlags.ReadOnly, aparams);
            byte[] script = scriptBuilder.ToArray();
            using var engine = ApplicationEngine.Run(script, snapshot, settings: protocolSettings, gas: restSettings.MaxInvokeGas);
            return engine;
        }

        public static ApplicationEngine InvokeScript(RestServerSettings restSettings, ReadOnlyMemory<byte> script, Signer[] signers = null, Witness[] witnesses = null)
        {
            var neosystem = RestServerPlugin.NeoSystem;
            var snapshot = neosystem.GetSnapshot();
            Transaction tx = signers == null ? null : new Transaction
            {
                Version = 0,
                Nonce = (uint)Random.Shared.Next(),
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + neosystem.Settings.MaxValidUntilBlockIncrement,
                Signers = signers,
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = script,
                Witnesses = witnesses
            };
            return ApplicationEngine.Run(script, snapshot, tx, settings: neosystem.Settings, gas: restSettings.MaxInvokeGas);
        }

        public static ContractMethodDescriptor GetContractMethod(DataCache snapshot, UInt160 scriptHash, string method, int pCount)
        {
            var contractState = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
            if (contractState == null)
                return null;
            return contractState.Manifest.Abi.GetMethod(method, pCount);
        }

        public static ContractParameter FromJson(JToken obj)
        {
            ContractParameter contractParam = new ContractParameter()
            {
                Type = Enum.Parse<ContractParameterType>(obj["type"].ToObject<string>()),
            };

            if (obj["value"] != null)
            {
                object value;
                switch (contractParam.Type)
                {
                    case ContractParameterType.ByteArray:
                    case ContractParameterType.Signature:
                        value = Convert.FromBase64String(obj["value"].ToObject<string>());
                        break;
                    case ContractParameterType.Boolean:
                        value = obj["value"].ToObject<bool>();
                        break;
                    case ContractParameterType.Integer:
                        value = BigInteger.Parse(obj["value"].ToObject<string>());
                        break;
                    case ContractParameterType.String:
                        value = obj["value"].ToObject<string>();
                        break;
                    case ContractParameterType.Hash160:
                        value = UInt160.Parse(obj["value"].ToObject<string>());
                        break;
                    case ContractParameterType.Hash256:
                        value = UInt256.Parse(obj["value"].ToObject<string>());
                        break;
                    case ContractParameterType.PublicKey:
                        value = ECPoint.Parse(obj["value"].ToObject<string>(), ECCurve.Secp256r1);
                        break;
                    case ContractParameterType.Array:
                        var a = obj["value"] as JArray;
                        value = a.Select(FromJson).ToList();
                        break;
                    case ContractParameterType.Map:
                        var m = obj["value"] as JArray;
                        value = m.Select(s => new KeyValuePair<ContractParameter, ContractParameter>(FromJson(s["key"]), FromJson(s["value"]))).ToList();
                        break;
                    default:
                        throw new ArgumentException(null, nameof(obj));
                }

                contractParam.Value = value;
            }

            return contractParam;
        }
    }
}
