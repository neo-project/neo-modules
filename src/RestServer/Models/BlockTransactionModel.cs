// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Models
{
    public class BlockTransactionModel
    {
        public UInt256 Hash { get; set; }
        public UInt160 Sender { get; set; }

        public ReadOnlyMemory<byte> Script { get; set; }

        public long FeePerByte { get; set; }
        public long NetworkFee { get; set; }
        public long SystemFee { get; set; }
        public int Size { get; set; }

        public uint Nonce { get; set; }
        public byte Version { get; set; }
        public uint ValidUntilBlock { get; set; }

        public IEnumerable<WitnessModel> Witnesses { get; set; }
        public IEnumerable<SignerModel> Signers { get; set; }
        public IEnumerable<TransactionAttributeModel> Attributes { get; set; }
    }
}
