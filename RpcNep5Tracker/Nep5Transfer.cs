using System.IO;
using System.Numerics;
using Neo.IO;

namespace Neo.Plugins
{
    public class Nep5Transfer : ICloneable<Nep5Transfer>, ISerializable
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public BigInteger Amount;

        int ISerializable.Size => 20 + sizeof(uint) + 32 + Amount.ToByteArray().GetVarSize();

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
            Amount = new BigInteger(reader.ReadVarBytes(512));
        }

        Nep5Transfer ICloneable<Nep5Transfer>.Clone()
        {
            return new Nep5Transfer
            {
                UserScriptHash = UserScriptHash,
                BlockIndex = BlockIndex,
                TxHash = TxHash,
                Amount = Amount
            };
        }

        void ICloneable<Nep5Transfer>.FromReplica(Nep5Transfer replica)
        {
            UserScriptHash = replica.UserScriptHash;
            BlockIndex = replica.BlockIndex;
            TxHash = replica.TxHash;
            Amount = replica.Amount;
        }
    }
}