// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class PrepareResponse : ConsensusMessage
    {
        public UInt256 PreparationHash;

        // priority or fallback
        public uint Id;
        public override int Size => base.Size + PreparationHash.Size;

        public PrepareResponse() : base(ConsensusMessageType.PrepareResponse) { }

        public override void Deserialize(ref MemoryReader reader)
        {
            base.Deserialize(ref reader);
            PreparationHash = reader.ReadSerializable<UInt256>();
            Id = reader.ReadUInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(PreparationHash);
            writer.Write(Id);
        }
    }
}
