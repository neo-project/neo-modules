// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Cryptography.MPTTrie
{
    public enum NodeType : byte
    {
        BranchNode = 0x00,
        ExtensionNode = 0x01,
        LeafNode = 0x02,
        HashNode = 0x03,
        Empty = 0x04
    }
}
