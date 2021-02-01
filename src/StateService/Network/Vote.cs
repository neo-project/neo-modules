
using System.IO;
using Neo.IO;

namespace Neo.Plugins.StateService.Network
{
    public class Vote : ISerializable
    {
        public int ValidatorIndex;
        public uint RootIndex;
        public byte[] Signature;

        public int Size => sizeof(int) + sizeof(uint) + Signature.GetVarSize();

        public Vote() { }

        public Vote(uint index, int validator, byte[] signature)
        {
            RootIndex = index;
            ValidatorIndex = validator;
            Signature = signature;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ValidatorIndex);
            writer.Write(RootIndex);
            writer.WriteVarBytes(Signature);
        }

        public void Deserialize(BinaryReader reader)
        {
            ValidatorIndex = reader.ReadInt32();
            RootIndex = reader.ReadUInt32();
            Signature = reader.ReadVarBytes();
        }
    }
}
