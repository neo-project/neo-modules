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
using System;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    class Signers : IVerifiable
    {
        private readonly Signer[] _signers;
        public Witness[] Witnesses { get; set; }
        public int Size => _signers.Length;

        public Signers(Signer[] signers)
        {
            _signers = signers;
        }

        public void Serialize(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public void DeserializeUnsigned(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public UInt160[] GetScriptHashesForVerifying(DataCache snapshot)
        {
            return _signers.Select(p => p.Account).ToArray();
        }

        public Signer[] GetSigners()
        {
            return _signers;
        }

        public void SerializeUnsigned(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
