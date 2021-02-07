using Neo.IO;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    class Vote : StateMessage
    {
        public int ValidatorIndex;
        public uint RootIndex;
        public byte[] Signature;

        public override int Size => base.Size + sizeof(int) + sizeof(uint) + Signature.GetVarSize();

        public Vote() : base(MessageType.Vote)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(ValidatorIndex);
            writer.Write(RootIndex);
            writer.WriteVarBytes(Signature);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            ValidatorIndex = reader.ReadInt32();
            RootIndex = reader.ReadUInt32();
            Signature = reader.ReadVarBytes(64);
        }
    }
}
