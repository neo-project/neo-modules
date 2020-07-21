using Neo.IO;
using Neo.Ledger;
using System.IO;

namespace Neo.Plugins
{
    public class UserSystemAssetTransfer : StateBase, ICloneable<UserSystemAssetTransfer>
    {
        public UInt160 UserScriptHash;
        public uint BlockIndex;
        public UInt256 TxHash;
        public Fixed8 Amount;

        public override int Size => base.Size + 20 + sizeof(uint) + 32 + Amount.Size;

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(UserScriptHash);
            writer.Write(BlockIndex);
            writer.Write(TxHash);
            writer.Write(Amount);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            UserScriptHash = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            TxHash = reader.ReadSerializable<UInt256>();
            Amount = reader.ReadSerializable<Fixed8>();
        }

        public UserSystemAssetTransfer Clone()
        {
            return new UserSystemAssetTransfer
            {
                UserScriptHash = UserScriptHash,
                BlockIndex = BlockIndex,
                TxHash = TxHash,
                Amount = Amount
            };
        }

        public void FromReplica(UserSystemAssetTransfer replica)
        {
            UserScriptHash = replica.UserScriptHash;
            BlockIndex = replica.BlockIndex;
            TxHash = replica.TxHash;
            Amount = replica.Amount;
        }
    }
}
