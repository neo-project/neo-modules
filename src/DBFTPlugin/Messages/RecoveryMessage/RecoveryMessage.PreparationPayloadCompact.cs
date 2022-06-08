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
    partial class RecoveryMessage
    {
        public class PreparationPayloadCompact : ISerializable
        {
            public byte ValidatorIndex;
            public ReadOnlyMemory<byte> InvocationScript;

            int ISerializable.Size =>
                sizeof(byte) +                  //ValidatorIndex
                InvocationScript.GetVarSize();  //InvocationScript

            void ISerializable.Deserialize(ref MemoryReader reader)
            {
                ValidatorIndex = reader.ReadByte();
                InvocationScript = reader.ReadVarMemory(1024);
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                writer.Write(ValidatorIndex);
                writer.WriteVarBytes(InvocationScript.Span);
            }
        }
    }
}
