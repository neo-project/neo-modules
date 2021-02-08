using Neo.IO;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    class Vote : ISerializable
    {
        public int ValidatorIndex;
        public uint RootIndex;
        public byte[] Signature;

        int ISerializable.Size => sizeof(int) + sizeof(uint) + Signature.GetVarSize();

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(ValidatorIndex);
            writer.Write(RootIndex);
            writer.WriteVarBytes(Signature);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            ValidatorIndex = reader.ReadInt32();
            RootIndex = reader.ReadUInt32();
            Signature = reader.ReadVarBytes(64);
        }
    }
}
