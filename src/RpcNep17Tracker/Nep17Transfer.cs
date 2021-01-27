using Neo.IO;
using System.IO;
using System.Numerics;

namespace Neo.Plugins
{
    public class Nep17Transfer : ISerializable
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public BigDecimal Amount;

        int ISerializable.Size => 20 + sizeof(uint) + 32 + Amount.Value.ToByteArray().GetVarSize() + sizeof(byte);

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(BlockIndex);
            writer.Write(TxHash);
            writer.WriteVarBytes(Amount.Value.ToByteArray());
            writer.Write(Amount.Decimals);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            UserScriptHash = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            TxHash = reader.ReadSerializable<UInt256>();
            var value = new BigInteger(reader.ReadVarBytes(512));
            var decimals = reader.ReadByte();
            Amount = new BigDecimal(value, decimals);
        }
    }
}
