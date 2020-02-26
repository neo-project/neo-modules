using System;

namespace Neo.Plugins.Storage
{
    internal static class Helper
    {
        public static byte[] CreateKey(byte table, byte[] key = null)
        {
            if (key is null) return new[] { table };
            byte[] buffer = new byte[1 + key.Length];
            buffer[0] = table;
            Buffer.BlockCopy(key, 0, buffer, 1, key.Length);
            return buffer;
        }
    }
}
