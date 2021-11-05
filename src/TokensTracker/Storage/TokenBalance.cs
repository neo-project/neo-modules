using Neo.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Plugins.Storage
{
    public class TokenBalance : ISerializable
    {
        public BigInteger Balance;
        public uint LastUpdatedBlock;

        int ISerializable.Size =>
            Balance.GetVarSize() +    // Balance
            sizeof(uint);               // LastUpdatedBlock

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Balance.ToByteArray());
            writer.Write(LastUpdatedBlock);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Balance = new BigInteger(reader.ReadVarBytes(512));
            LastUpdatedBlock = reader.ReadUInt32();
        }
    }
}
