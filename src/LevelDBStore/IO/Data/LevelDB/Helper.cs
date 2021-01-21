using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Neo.IO.Data.LevelDB
{
    public static class Helper
    {
        public static IEnumerable<T> Seek<T>(this DB db, ReadOptions options, byte[] prefix, SeekDirection direction, Func<byte[], byte[], T> resultSelector)
        {
            using Iterator it = db.NewIterator(options);
            if (direction == SeekDirection.Forward)
            {
                for (it.Seek(prefix); it.Valid(); it.Next())
                    yield return resultSelector(it.Key(), it.Value());
            }
            else
            {
                // SeekForPrev

                it.Seek(prefix);
                if (!it.Valid())
                    it.SeekToLast();
                else if (it.Key().AsSpan().SequenceCompareTo(prefix) > 0)
                    it.Prev();

                for (; it.Valid(); it.Prev())
                    yield return resultSelector(it.Key(), it.Value());
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
