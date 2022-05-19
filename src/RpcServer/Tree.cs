// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    class Tree<T>
    {
        public TreeNode<T> Root { get; private set; }

        public TreeNode<T> AddRoot(T item)
        {
            if (Root is not null)
                throw new InvalidOperationException();
            Root = new TreeNode<T>(item, null);
            return Root;
        }

        public IEnumerable<T> GetItems()
        {
            if (Root is null) yield break;
            foreach (T item in Root.GetItems())
                yield return item;
        }
    }
}
