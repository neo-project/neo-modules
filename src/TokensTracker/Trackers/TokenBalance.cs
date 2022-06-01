// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Plugins.TokensTracker is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System.IO;
using System.Numerics;

namespace Neo.Plugins.Trackers
{
    public class TokenBalance : ISerializable
    {
        public BigInteger Balance;
        public uint LastUpdatedBlock;

        int ISerializable.Size =>
            Balance.GetVarSize() +    // Balance
            sizeof(uint);             // LastUpdatedBlock

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Balance.ToByteArray());
            writer.Write(LastUpdatedBlock);
        }

        void ISerializable.Deserialize(ref MemoryReader reader)
        {
            Balance = new BigInteger(reader.ReadVarMemory(32).Span);
            LastUpdatedBlock = reader.ReadUInt32();
        }
    }
}
