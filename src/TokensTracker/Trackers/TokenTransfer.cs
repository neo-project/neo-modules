using System.IO;
using System.Numerics;
using Neo.IO;

namespace Neo.Plugins.Trackers
{
    public class TokenTransfer : ISerializable
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public BigInteger Amount;

        int ISerializable.Size =>
            UInt160.Length +        // UserScriptHash
            sizeof(uint) +          // BlockIndex
            UInt256.Length +        // TxHash
            Amount.GetVarSize();    // Amount

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(BlockIndex);
            writer.Write(TxHash);
            writer.WriteVarBytes(Amount.ToByteArray());
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            UserScriptHash = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            TxHash = reader.ReadSerializable<UInt256>();
            Amount = new BigInteger(reader.ReadVarBytes(32));
        }
    }
}
