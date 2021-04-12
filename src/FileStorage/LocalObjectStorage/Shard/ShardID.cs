using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.Cryptography;


namespace Neo.FileStorage.LocalObjectStorage.Shard
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
