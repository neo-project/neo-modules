using Neo.IO;
using System.IO;
using System.Numerics;

namespace Neo.Plugins
{
    public class Nep17Balance : ISerializable
    {
        public BigDecimal Balance;
        public uint LastUpdatedBlock;

        int ISerializable.Size => Balance.Value.ToByteArray().GetVarSize() + sizeof(byte) + sizeof(uint);

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Balance.Value.ToByteArray());
            writer.Write(Balance.Decimals);
            writer.Write(LastUpdatedBlock);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            var value = new BigInteger(reader.ReadVarBytes(512));
            var decimals = reader.ReadByte();
            Balance = new BigDecimal(value, decimals);
            LastUpdatedBlock = reader.ReadUInt32();
        }
    }
}
