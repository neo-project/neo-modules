using System;
using Neo.Cryptography;


namespace Neo.FileStorage.LocalObjectStorage.Shards
{
    public class ShardID
    {
        private readonly byte[] value;

        public bool IsEmpty => value is null || value.Length == 0;

        public ShardID(byte[] bytes)
        {
            value = bytes;
        }

        public override string ToString()
        {
            return Base58.Encode(value);
        }

        public static ShardID Generate()
        {
            return Guid.NewGuid().ToByteArray();
        }

        public static implicit operator ShardID(byte[] val)
        {
            return new ShardID(val);
        }

        public static implicit operator byte[](ShardID b)
        {
            return b.value;
        }
    }
}
