// Copyright (C) 2015-2021 The Neo Project.
//
//  The neo is free software distributed under the MIT software license, 
//  see the accompanying file LICENSE in the main directory of the
//  project or http://www.opensource.org/licenses/mit-license.php 
//  for more details. 
//  Redistribution and use in source and binary forms with or without
//  modifications are permitted.

using Neo.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins.Storage
{
    public class Nep11Transfer : ISerializable
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public BigInteger Amount;

        int ISerializable.Size =>
            UInt160.Length +        // UserScriptHash
            sizeof(uint) +          // BlockIndex
            UInt256.Length +        // TxHash
            Amount.GetByteCount();  // Amount

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(BlockIndex);
            writer.Write(TxHash);
            writer.WriteVarBytes(Amount.ToByteArray());
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            UserScriptHash = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            TxHash = reader.ReadSerializable<UInt256>();
            Amount = new BigInteger(reader.ReadVarBytes(32));
        }
    }
}
