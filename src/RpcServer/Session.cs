// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    class Session : IDisposable
    {
        public static readonly TimeSpan ExpirationTime = TimeSpan.FromMinutes(5);

        public readonly SnapshotCache Snapshot;
        public readonly ApplicationEngine Engine;
        public readonly Dictionary<Guid, IIterator> Iterators = new();
        public DateTime Expiration;

        public Session(NeoSystem system, byte[] script, Signers signers, long gas, Diagnostic diagnostic)
        {
            Random random = new();
            Snapshot = system.GetSnapshot();
            Transaction tx = signers == null ? null : new Transaction
            {
                Version = 0,
                Nonce = (uint)random.Next(),
                ValidUntilBlock = NativeContract.Ledger.CurrentIndex(Snapshot) + system.Settings.MaxValidUntilBlockIncrement,
                SystemFee = gas,
                NetworkFee = 1_00000000,
                Signers = signers.GetSigners(),
                Attributes = Array.Empty<TransactionAttribute>(),
                Script = script,
                Witnesses = signers.Witnesses
            };
            Engine = ApplicationEngine.Run(script, Snapshot, container: tx, settings: system.Settings, gas: gas, diagnostic: diagnostic);
            ResetExpiration();
        }

        public void ResetExpiration()
        {
            Expiration = DateTime.UtcNow + ExpirationTime;
        }

        public void Dispose()
        {
            Engine.Dispose();
            Snapshot.Dispose();
        }
    }
}
