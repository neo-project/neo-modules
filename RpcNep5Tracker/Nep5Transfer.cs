using System.IO;
using System.Numerics;
using Neo.IO;
using Neo.Ledger;

namespace Neo.Plugins
{
    public class Nep5Transfer : StateBase, ICloneable<Nep5Transfer>
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public BigInteger Amount;

        public override int Size => base.Size + 20 + sizeof(uint) + 32 + Amount.ToByteArray().GetVarSize();

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(UserScriptHash);
            writer.Write(BlockIndex);
            writer.Write(TxHash);
            writer.WriteVarBytes(Amount.ToByteArray());
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            UserScriptHash = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            TxHash = reader.ReadSerializable<UInt256>();
            Amount = new BigInteger(reader.ReadVarBytes(512));
        }

        public Nep5Transfer Clone()
        {
            return new Nep5Transfer
            {
                UserScriptHash = UserScriptHash,
                BlockIndex = BlockIndex,
                TxHash = TxHash,
                Amount = Amount
            };
        }

        public void FromReplica(Nep5Transfer replica)
        {
            UserScriptHash = replica.UserScriptHash;
            BlockIndex = replica.BlockIndex;
            TxHash = replica.TxHash;
            Amount = replica.Amount;
        }
    }
}