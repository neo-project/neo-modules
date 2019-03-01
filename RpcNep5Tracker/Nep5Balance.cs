using System.IO;
using System.Numerics;
using Neo.IO;
using Neo.Ledger;

namespace Neo.Plugins
{
    public class Nep5Balance : StateBase, ICloneable<Nep5Balance>
    {
        public BigInteger Balance;
        public uint LastUpdatedBlock;

        public override int Size => base.Size + Balance.ToByteArray().GetVarSize();

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.WriteVarBytes(Balance.ToByteArray());
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Balance = new BigInteger(reader.ReadVarBytes(512));
        }

        public Nep5Balance Clone()
        {
            return new Nep5Balance
            {
                Balance = Balance,
                LastUpdatedBlock = LastUpdatedBlock
            };
        }

        public void FromReplica(Nep5Balance replica)
        {
            Balance = replica.Balance;
            LastUpdatedBlock = replica.LastUpdatedBlock;
        }
    }
}