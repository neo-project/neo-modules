using Neo.IO;
using Neo.IO.Data.LevelDB;
using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    internal static class Helper
    {
        public static IEnumerable<(TKey, TValue)> FindRange<TKey, TValue>(this DB db, byte[] startKeyBytes, byte[] endKeyBytes)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ISerializable, new()
        {
            return db.FindRange(ReadOptions.Default, startKeyBytes, endKeyBytes, (k, v) => (k.AsSerializable<TKey>(1), v.AsSerializable<TValue>()));
        }
    }
}
