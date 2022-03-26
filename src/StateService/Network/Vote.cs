// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Plugins.StateService is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    class Vote : ISerializable
    {
        public int ValidatorIndex;
        public uint RootIndex;
        public byte[] Signature;

        int ISerializable.Size => sizeof(int) + sizeof(uint) + Signature.GetVarSize();

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(ValidatorIndex);
            writer.Write(RootIndex);
            writer.WriteVarBytes(Signature);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            ValidatorIndex = reader.ReadInt32();
            RootIndex = reader.ReadUInt32();
            Signature = reader.ReadVarBytes(64);
        }
    }
}
