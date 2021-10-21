// Copyright (C) 2015-2021 The Neo Project.
//
//  The neo is free software distributed under the MIT software license, 
//  see the accompanying file LICENSE in the main directory of the
//  project or http://www.opensource.org/licenses/mit-license.php 
//  for more details. 
//  Redistribution and use in source and binary forms with or without
//  modifications are permitted.

using Neo.IO;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.Persistence;

namespace Neo.Plugins
{
    public static class Extensions
    {
        public static bool NotNull(this StackItem item)
        {
            return !item.IsNull;
        }

        public static string ToBase64(this ReadOnlySpan<byte> item)
        {
            return item == null ? String.Empty : Convert.ToBase64String(item);
        }

        public static int GetVarSize(this ByteString item)
        {
            var length = item.GetSpan().Length;
            return IO.Helper.GetVarSize(length) + length;
        }

        public static IEnumerable<(TKey, TValue)> FindPrefix<TKey, TValue>(this IStore db, byte[] prefix)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ISerializable, new()
        {
            foreach (var (key, value) in db.Seek(prefix, SeekDirection.Forward))
            {
                if (!key.AsSpan().StartsWith(prefix)) break;
                yield return (key.AsSerializable<TKey>(1), value.AsSerializable<TValue>());
            }


        }

        public static IEnumerable<(TKey, TValue)> FindRange<TKey, TValue>(this IStore db, byte[] startKey, byte[] endKey)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ISerializable, new()
        {
            foreach (var (key, value) in db.Seek(startKey, SeekDirection.Forward))
            {
                if (key.AsSpan().SequenceCompareTo(endKey) > 0) break;
                yield return (key.AsSerializable<TKey>(1), value.AsSerializable<TValue>());
            }
        }
    }
}
