using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public static class Extensions
    {
        public static bool NotNull(this StackItem item)
        {
            return !item.IsNull;
        }

        public static IEnumerable<(TKey, TValue)> FindRange<TKey, TValue>(this DB db, byte[] startKeyBytes, byte[] endKeyBytes)
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : class, ISerializable, new()
        {
            return db.FindRange(ReadOptions.Default, startKeyBytes, endKeyBytes, (k, v) => (k.AsSerializable<TKey>(1), v.AsSerializable<TValue>()));
        }
    }
}
