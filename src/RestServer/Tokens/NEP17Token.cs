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
using Neo.VM;
using System.Numerics;
using Neo.Plugins.RestServer.Helpers;

namespace Neo.Plugins.RestServer.Tokens
{
    internal class NEP17Token
    {
        public UInt160 ScriptHash { get; init; }
        public string Name { get; init; }
        public string Symbol { get; init; }
        public byte Decimals { get; init; }

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;
        private readonly DataCache _datacache;

        public NEP17Token(
            NeoSystem neoSystem,
            UInt160 scriptHash,
            RestServerSettings settings,
            DataCache snapshot = null)
        {
            _settings = settings;
            _datacache = snapshot ?? neoSystem.GetSnapshot();
            var contractState = NativeContract.ContractManagement.GetContract(_datacache, scriptHash) ?? throw new ArgumentException(null, nameof(scriptHash));
            if (ContractHelper.IsNep17Supported(contractState) == false) throw new NotSupportedException(nameof(scriptHash));
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(scriptHash, "decimals", CallFlags.ReadOnly);
                sb.EmitDynamicCall(scriptHash, "symbol", CallFlags.ReadOnly);
                script = sb.ToArray();
            }
            using var engine = ApplicationEngine.Run(script, _datacache, settings: neoSystem.Settings, gas: settings.MaxInvokeGas);
            if (engine.State != VMState.HALT) throw engine.FaultException;

            _neosystem = neoSystem;
            ScriptHash = scriptHash;
            Name = contractState.Manifest.Name;
            Symbol = engine.ResultStack.Pop().GetString();
            Decimals = (byte)engine.ResultStack.Pop().GetInteger();
        }

        public BigDecimal BalanceOf(UInt160 address)
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _datacache, ScriptHash, "balanceOf", out var result, address))
                return new BigDecimal(result[0].GetInteger(), Decimals);
            return new BigDecimal(BigInteger.Zero, Decimals);
        }

        public BigDecimal TotalSupply()
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _datacache, ScriptHash, "totalSupply", out var result))
                return new BigDecimal(result[0].GetInteger(), Decimals);
            return new BigDecimal(BigInteger.Zero, Decimals);
        }
    }
}
