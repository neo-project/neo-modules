using System.IO;
using System.Numerics;
using Neo.IO;

namespace Neo.Plugins
{
    public class Nep17Balance : ICloneable<Nep17Balance>, ISerializable
    {
        public BigInteger Balance;
        public uint LastUpdatedBlock;

        int ISerializable.Size => Balance.ToByteArray().GetVarSize() + sizeof(uint);

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Balance.ToByteArray());
            writer.Write(LastUpdatedBlock);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Balance = new BigInteger(reader.ReadVarBytes(512));
            LastUpdatedBlock = reader.ReadUInt32();
        }

        Nep17Balance ICloneable<Nep17Balance>.Clone()
        {
            return new Nep17Balance
            {
                Balance = Balance,
                LastUpdatedBlock = LastUpdatedBlock
            };
        }

        public void FromReplica(Nep17Balance replica)
        {
            Balance = replica.Balance;
            LastUpdatedBlock = replica.LastUpdatedBlock;
        }
    }
}
