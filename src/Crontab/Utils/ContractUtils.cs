// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Cryptography.ECC;
using Neo.Plugins.Crontab.Jobs;
using Neo.Plugins.Crontab.Settings;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Numerics;

namespace Neo.Plugins.Crontab.Utils;

internal static class ContractUtils
{
    public static bool MethodExists(UInt160 scriptHash, string methodName, int parameterCount = -1)
    {
        var contract = NativeContract.ContractManagement.GetContract(CronPlugin.NeoSystem.StoreView, scriptHash);
        if (contract == null)
            return false;
        else
            return contract.Manifest.Abi.GetMethod(methodName, parameterCount) != null;
    }

    public static bool BuildInvokeMethod(CronContract cronContract, out byte[] script)
    {
        script = null;
        if (MethodExists(cronContract.ScriptHash, cronContract.Method, cronContract.Params.Length) == false)
            return false;
        else
        {
            var args = cronContract.Params.Select(ConvertParameters).ToArray();
            using var sb = new ScriptBuilder();
            if (args.Length > 0)
                sb.EmitDynamicCall(cronContract.ScriptHash, cronContract.Method, args);
            else
                sb.EmitDynamicCall(cronContract.ScriptHash, cronContract.Method);

            script = sb.ToArray();

            return true;
        }
    }

    public static ContractParameter ConvertParameters(CronJobContractParameterSettings parameterSettings)
    {
        return parameterSettings.Type switch
        {
            ContractParameterType.ByteArray => new()
            {
                Type = ContractParameterType.ByteArray,
                Value = Convert.FromBase64String(parameterSettings.Value),
            },
            ContractParameterType.Signature => new()
            {
                Type = ContractParameterType.Signature,
                Value = Convert.FromBase64String(parameterSettings.Value),
            },
            ContractParameterType.Boolean => new()
            {
                Type = ContractParameterType.Boolean,
                Value = bool.Parse(parameterSettings.Value),
            },
            ContractParameterType.Integer => new()
            {
                Type = ContractParameterType.Integer,
                Value = BigInteger.Parse(parameterSettings.Value),
            },
            ContractParameterType.String => new()
            {
                Type = ContractParameterType.String,
                Value = parameterSettings.Value,
            },
            ContractParameterType.Hash160 => new()
            {
                Type = ContractParameterType.Hash160,
                Value = UInt160.Parse(parameterSettings.Value),
            },
            ContractParameterType.Hash256 => new()
            {
                Type = ContractParameterType.Hash256,
                Value = UInt256.Parse(parameterSettings.Value),
            },
            ContractParameterType.PublicKey => new()
            {
                Type = ContractParameterType.PublicKey,
                Value = ECPoint.Parse(parameterSettings.Value, ECCurve.Secp256r1),
            },
            _ => throw new NotSupportedException($"{parameterSettings.Type} is not supported.")
        };
    }
}
