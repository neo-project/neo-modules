// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.Cryptography.MPTTrie
{
    public static class Helper
    {
        public static int CompareTo(this byte[] arr1, byte[] arr2)
        {
            if (arr1 is null || arr2 is null) throw new ArgumentNullException();
            for (int i = 0; i < arr1.Length && i < arr2.Length; i++)
            {
                var r = arr1[i].CompareTo(arr2[i]);
                if (r != 0) return r;
            }
            return arr2.Length < arr1.Length ? 1 : arr2.Length == arr1.Length ? 0 : -1;
        }
    }
}
