using System;
using System.Buffers.Binary;

namespace Neo.Plugins.StateService.Storage
{
    public static class Keys
    {
        public static byte[] StateRoot(uint index)
        {
            byte[] buffer = new byte[sizeof(uint) + 1];
            buffer[0] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(1), index);
            return buffer;
        }

        public static readonly byte[] CurrentLocalRootIndex = { 0x02 };
        public static readonly byte[] CurrentValidatedRootIndex = { 0x04 };
    }
}
