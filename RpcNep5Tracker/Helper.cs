using System;
using System.Collections.Generic;
using Neo.IO;
using Neo.IO.Data.LevelDB;

namespace Neo.Plugins
{
    internal static class Helper
    {
        public static IEnumerable<T> FindRange<T>(this DB db, ReadOptions options, Slice startKey, Slice endKey, Func<Slice, Slice, T> resultSelector)
        {
            using (Iterator it = db.NewIterator(options))
            {
                for (it.Seek(startKey); it.Valid(); it.Next())
                {
                    Slice key = it.Key();
                    if (key > endKey) break;
                    yield return resultSelector(key, it.Value());
                }
            }
        }

        public static IEnumerable<KeyValuePair<TKey, TValue>> FindRange<TKey, TValue>(this DB db, byte[] startKeyBytes, byte[] endKeyBytes)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ICloneable<TValue>, ISerializable, new()
        {
            return db.FindRange(ReadOptions.Default, SliceBuilder.Begin().Add(startKeyBytes),
                SliceBuilder.Begin().Add(endKeyBytes),
                (k, v) => new KeyValuePair<TKey, TValue>(k.ToArray().AsSerializable<TKey>(1),
                    v.ToArray().AsSerializable<TValue>()));
        }
    }
}