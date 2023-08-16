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
    public class BlockHeaderModel
    {
        public ulong Timestamp { get; set; }
        public uint Version { get; set; }
        public byte PrimaryIndex { get; set; }
        public uint Index { get; set; }
        public ulong Nonce { get; set; }
        public UInt256 Hash { get; set; }
        public UInt256 MerkleRoot { get; set; }
        public UInt256 PrevHash { get; set; }
        public UInt160 NextConsensus { get; set; }
        public WitnessModel Witness { get; set; }
        public int Size { get; set; }
    }
}
