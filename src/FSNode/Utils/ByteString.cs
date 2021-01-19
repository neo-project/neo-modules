using Google.Protobuf;
using System;

namespace Neo.FSNode.Utils
{
    public static partial class Utils
    {
        public static ByteString Concat(this ByteString a, ByteString b)
        {
            if (a is null)
                throw new ArgumentNullException(nameof(a));
            if (a is null)
                throw new ArgumentNullException(nameof(b));
            return ByteString.CopyFrom(Helper.Concat(a.ToByteArray(), b.ToByteArray()));
        }

        public static ByteString Range(this ByteString a, int left, int right)
        {
            if (a is null)
                throw new ArgumentNullException(nameof(a));
            return ByteString.CopyFrom(a.ToByteArray()[left..right]);
        }
    }
}
