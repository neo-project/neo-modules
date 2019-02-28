using System.IO;
using System.Numerics;
using Neo.IO;
using Neo.Ledger;

namespace Neo.Plugins
{
    public class Nep5Transfer : StateBase, ICloneable<Nep5Transfer>
    {
        public UInt160 UserScriptHash;
        public BigInteger Amount;

        public override int Size => base.Size + 20 + Amount.ToByteArray().GetVarSize();

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(UserScriptHash);
            writer.WriteVarBytes(Amount.ToByteArray());
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            UserScriptHash = reader.ReadSerializable<UInt160>();
            Amount = new BigInteger(reader.ReadVarBytes(512));
        }

        public Nep5Transfer Clone()
        {
            return new Nep5Transfer
            {
                UserScriptHash = UserScriptHash,
                Amount = Amount,
            };
        }

        public void FromReplica(Nep5Transfer replica)
        {
            UserScriptHash = replica.UserScriptHash;
            Amount = replica.Amount;
        }
    }
}