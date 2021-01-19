using System;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public static class Util
    {
        public static ulong RandomUInt64(ulong max)
        {
            var random = new Random();
            var buffer = new byte[sizeof(ulong)];
            random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer) % max;
        }
    }
}
