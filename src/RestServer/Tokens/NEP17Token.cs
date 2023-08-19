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
        public UInt160 TokenHash { get; init; }
        public string TokenName { get; init; }
        public string Symbol { get; init; }
        public byte Decimals { get; init; }

        private readonly NeoSystem _neosystem;
        private readonly DataCache _datacache;

        public NEP17Token(
            NeoSystem neoSystem,
            UInt160 tokenHash,
            DataCache snapshot = null)
        {
            _datacache = snapshot ?? neoSystem.GetSnapshot();
            var contractState = NativeContract.ContractManagement.GetContract(_datacache, tokenHash) ?? throw new ArgumentException(null, nameof(tokenHash));
            if (ContractHelper.IsNep17Supported(contractState) == false) throw new NotSupportedException(nameof(tokenHash));
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(tokenHash, "decimals", CallFlags.ReadOnly);
                sb.EmitDynamicCall(tokenHash, "symbol", CallFlags.ReadOnly);
                script = sb.ToArray();
            }
            using var engine = ApplicationEngine.Run(script, _datacache, settings: neoSystem.Settings, gas: 0_30000000L);
            if (engine.State != VMState.HALT) throw engine.FaultException;

            this._neosystem = neoSystem;
            this.TokenHash = tokenHash;
            this.TokenName = contractState.Manifest.Name;
            this.Symbol = engine.ResultStack.Pop().GetString();
            this.Decimals = (byte)engine.ResultStack.Pop().GetInteger();
        }

        public BigDecimal BalanceOf(UInt160 address)
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _datacache, TokenHash, "balanceOf", out var result, address))
                return new BigDecimal(result[0].GetInteger(), Decimals);
            return new BigDecimal(BigInteger.Zero, Decimals);
        }

        public BigDecimal TotalSupply()
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _datacache, TokenHash, "totalSupply", out var result))
                return new BigDecimal(result[0].GetInteger(), Decimals);
            return new BigDecimal(BigInteger.Zero, Decimals);
        }
    }
}
