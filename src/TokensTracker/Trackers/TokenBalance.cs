using System.IO;
using System.Numerics;
using Neo.IO;

namespace Neo.Plugins.Trackers
{
    public class TokenBalance : ISerializable
    {
        public BigInteger Balance;
        public uint LastUpdatedBlock;

        int ISerializable.Size =>
            Balance.GetVarSize() +    // Balance
            sizeof(uint);             // LastUpdatedBlock

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Balance.ToByteArray());
            writer.Write(LastUpdatedBlock);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Balance = new BigInteger(reader.ReadVarBytes(32));
            LastUpdatedBlock = reader.ReadUInt32();
        }
    }
}
