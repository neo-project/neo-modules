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
using Neo.Plugins.RestServer.Helpers;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.SmartContract;
using Neo.VM.Types;
using Neo.VM;
using System.Numerics;

namespace Neo.Plugins.RestServer.Tokens
{
    internal class NEP11Token
    {
        public UInt160 ScriptHash { get; private set; }
        public string Name { get; private set; }
        public string Symbol { get; private set; }
        public byte Decimals { get; private set; }

        private readonly NeoSystem _neosystem;
        private readonly DataCache _snapshot;
        private readonly ContractState _contract;
        private readonly RestServerSettings _settings;

        public NEP11Token(
            NeoSystem neoSystem,
            UInt160 scriptHash,
            RestServerSettings settings) : this(neoSystem, null, scriptHash, settings) { }

        public NEP11Token(
            NeoSystem neoSystem,
            DataCache snapshot,
            UInt160 scriptHash,
            RestServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(neoSystem, nameof(neoSystem));
            ArgumentNullException.ThrowIfNull(scriptHash, nameof(scriptHash));
            _neosystem = neoSystem;
            _snapshot = snapshot ?? _neosystem.GetSnapshot();
            _contract = NativeContract.ContractManagement.GetContract(_snapshot, scriptHash) ?? throw new ArgumentException(null, nameof(scriptHash));
            Name = _contract.Manifest.Name;
            ScriptHash = scriptHash;
            _settings = settings;
            Initialize();
        }

        private void Initialize()
        {
            byte[] scriptBytes;
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(_contract.Hash, "decimals", CallFlags.ReadOnly);
            sb.EmitDynamicCall(_contract.Hash, "symbol", CallFlags.ReadOnly);
            scriptBytes = sb.ToArray();

            using var appEngine = ApplicationEngine.Run(scriptBytes, _snapshot, settings: _neosystem.Settings, gas: _settings.MaxInvokeGas);
            if (appEngine.State != VMState.HALT) throw appEngine.FaultException;

            Symbol = appEngine.ResultStack.Pop().GetString();
            Decimals = (byte)appEngine.ResultStack.Pop().GetInteger();
        }

        public BigDecimal TotalSupply()
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "totalSupply", out var results))
                return new(results[0].GetInteger(), Decimals);
            return new(BigInteger.Zero, 0);
        }

        public BigDecimal BalanceOf(UInt160 address)
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "balanceOf", out var results, address))
                return new(results[0].GetInteger(), Decimals);
            return new(BigInteger.Zero, 0);
        }

        public BigDecimal BalanceOf(UInt160 address, byte[] tokenId)
        {
            if (Decimals == 0) throw new InvalidOperationException();
            ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
            if (tokenId.Length > 64) throw new ArgumentOutOfRangeException(nameof(tokenId));
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "balanceOf", out var results, address, tokenId))
                return new(results[0].GetInteger(), Decimals);
            return new(BigInteger.Zero, 0);
        }

        public byte[][] TokensOf(UInt160 owner)
        {
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "tokensOf", out var results, owner))
            {
                if (results[0].GetInterface<object>() is IIterator iterator)
                {
                    var refCounter = new ReferenceCounter();
                    var lstTokens = new List<byte[]>();
                    while (iterator.Next())
                        lstTokens.Add(iterator.Value(refCounter).GetSpan().ToArray());
                    return lstTokens.ToArray();
                }
            }
            return System.Array.Empty<byte[]>();
        }

        public UInt160[] OwnerOf(byte[] tokenId)
        {
            ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
            if (tokenId.Length > 64) throw new ArgumentOutOfRangeException(nameof(tokenId));
            if (Decimals == 0)
            {
                if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "ownerOf", out var results, tokenId))
                    return new[] { new UInt160(results[0].GetSpan()) };
            }
            else if (Decimals > 0)
            {
                if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "ownerOf", out var results, tokenId))
                {
                    if (results[0].GetInterface<object>() is IIterator iterator)
                    {
                        var refCounter = new ReferenceCounter();
                        var lstOwners = new List<UInt160>();
                        while (iterator.Next())
                            lstOwners.Add(new UInt160(iterator.Value(refCounter).GetSpan()));
                        return lstOwners.ToArray();
                    }
                }
            }
            return System.Array.Empty<UInt160>();
        }

        public byte[][] Tokens()
        {
            try
            {
                if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "tokens", out var results))
                {
                    if (results[0].GetInterface<object>() is IIterator iterator)
                    {
                        var refCounter = new ReferenceCounter();
                        var lstTokenIds = new List<byte[]>();
                        while (iterator.Next())
                            lstTokenIds.Add(iterator.Value(refCounter).GetSpan().ToArray());
                        return lstTokenIds.ToArray();
                    }
                }
            }
            catch
            {
            }
            return System.Array.Empty<byte[]>();
        }

        public Dictionary<string, StackItem> Properties(byte[] tokenId)
        {
            ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
            if (tokenId.Length > 64) throw new ArgumentOutOfRangeException(nameof(tokenId));
            if (ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _snapshot, ScriptHash, "properties", out var results, tokenId))
            {
                if (results[0] is Map map)
                {
                    return map.ToDictionary(key => key.Key.GetString(), value => value.Value);
                }
            }
            return default;
        }
    }
}
