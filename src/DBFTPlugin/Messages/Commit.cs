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
using System;
using System.IO;

namespace Neo.Consensus
{
    public class Commit : ConsensusMessage
    {
        public ReadOnlyMemory<byte> Signature;

        // priority or fallback
        public uint Id;

        public override int Size => base.Size + Signature.Length + sizeof(uint);

        public Commit() : base(ConsensusMessageType.Commit) { }

        public override void Deserialize(ref MemoryReader reader)
        {
            base.Deserialize(ref reader);
            Signature = reader.ReadMemory(64);
            Id = reader.ReadUInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature.Span);
            writer.Write(Id);
        }
    }
}
