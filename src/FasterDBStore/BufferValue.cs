using FASTER.core;

namespace Neo.Plugins.Storage
{
    internal class BufferValue : IFasterEqualityComparer<BufferValue>
    {
        public byte[] Value = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public BufferValue() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        public BufferValue(byte[] value)
        {
            Value = value;
        }

        public long GetHashCode64(ref BufferValue key)
        {
            return Unsafe.GetHashCode(key.Value);
        }

        public bool Equals(ref BufferValue v1, ref BufferValue v2)
        {
            return Unsafe.MemoryEquals(v1.Value, v2.Value);
        }
    }
}
