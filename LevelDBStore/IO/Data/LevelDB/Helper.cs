using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Neo.IO.Data.LevelDB
{
    public static class Helper
    {
        public static IEnumerable<T> Find<T>(this DB db, ReadOptions options, byte[] prefix, Func<byte[], byte[], T> resultSelector)
        {
            using Iterator it = db.NewIterator(options);
            for (it.Seek(prefix); it.Valid(); it.Next())
            {
                byte[] key = it.Key();
                if (key.Length < prefix.Length) break;
                if (!key.AsSpan().StartsWith(prefix)) break;
                yield return resultSelector(key, it.Value());
            }
        }

        public static IEnumerable<T> FindRange<T>(this DB db, ReadOptions options, byte[] startKey, byte[] endKey, Func<byte[], byte[], T> resultSelector)
        {
            using Iterator it = db.NewIterator(options);
            for (it.Seek(startKey); it.Valid(); it.Next())
            {
                byte[] key = it.Key();
                if (key.AsSpan().SequenceCompareTo(endKey) > 0) break;
                yield return resultSelector(key, it.Value());
            }
        }

        internal static byte[] ToByteArray(this IntPtr data, UIntPtr length)
        {
            if (data == IntPtr.Zero) return null;
            byte[] buffer = new byte[(int)length];
            Marshal.Copy(data, buffer, 0, (int)length);
            return buffer;
        }
    }
}
