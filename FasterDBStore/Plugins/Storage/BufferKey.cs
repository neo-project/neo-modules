using FASTER.core;
using Neo.Plugins.Storage.Helper;

namespace Neo.Plugins.Storage
{
    internal class BufferKey : IFasterEqualityComparer<BufferKey>
    {
        public byte Table;
        public byte[] Key;

        /// <summary>
        /// Constructor
        /// </summary>
        public BufferKey() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="table">Table</param>
        /// <param name="key">Key</param>
        public BufferKey(byte table, byte[] key)
        {
            Table = table;
            Key = key;
        }

        public long GetHashCode64(ref BufferKey key)
        {
            return Table + Unsafe.GetHashCode(key.Key);
        }

        public bool Equals(ref BufferKey key1, ref BufferKey key2)
        {
            return key1.Table == key2.Table && Unsafe.MemoryEquals(key1.Key, key2.Key);
        }
    }
}
