// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.IO;

namespace Neo.Cryptography.MPTTrie
{
    partial class Node
    {
        public const int BranchChildCount = 17;
        public Node[] Children;

        public static Node NewBranch()
        {
            var n = new Node
            {
                type = NodeType.BranchNode,
                Reference = 1,
                Children = new Node[BranchChildCount],
            };
            for (int i = 0; i < BranchChildCount; i++)
            {
                n.Children[i] = new Node();
            }
            return n;
        }

        protected int BranchSize
        {
            get
            {
                int size = 0;
                for (int i = 0; i < BranchChildCount; i++)
                {
                    size += Children[i].SizeAsChild;
                }
                return size;
            }
        }

        private void SerializeBranch(BinaryWriter writer)
        {
            for (int i = 0; i < BranchChildCount; i++)
            {
                Children[i].SerializeAsChild(writer);
            }
        }

        private void DeserializeBranch(BinaryReader reader)
        {
            Children = new Node[BranchChildCount];
            for (int i = 0; i < BranchChildCount; i++)
            {
                var n = new Node();
                n.Deserialize(reader);
                Children[i] = n;
            }
        }
    }
}
