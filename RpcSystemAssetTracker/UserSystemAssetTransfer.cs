using Neo.IO;
using Neo.Ledger;
using System.IO;

namespace Neo.Plugins
{
    public class UserSystemAssetTransfer : StateBase, ICloneable<UserSystemAssetTransfer>
    {
        public uint BlockIndex;
        public Fixed8 Amount;
        public override int Size => base.Size + sizeof(uint) + Amount.Size;

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(BlockIndex);
            writer.Write(Amount);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            BlockIndex = reader.ReadUInt32();
            Amount = reader.ReadSerializable<Fixed8>();
        }

        public UserSystemAssetTransfer Clone()
        {
            return new UserSystemAssetTransfer
            {
                BlockIndex = this.BlockIndex,
                Amount = this.Amount
            };
        }

        public void FromReplica(UserSystemAssetTransfer replica)
        {
            BlockIndex = replica.BlockIndex;
            Amount = replica.Amount;
        }
    }
}
