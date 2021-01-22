using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins.StateService.Tests
{
    public static class Helper
    {
        private static readonly byte Prefix = 0xf0;

        public static byte[] ToKey(this UInt256 hash)
        {
            byte[] buffer = new byte[UInt256.Length + 1];
            using (MemoryStream ms = new MemoryStream(buffer, true))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(Prefix);
                hash.Serialize(writer);
            }
            return buffer;
        }
    }
}
