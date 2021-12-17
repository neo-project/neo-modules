using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class Commit : ConsensusMessage
    {
        public byte[] Signature;

        // priority or fallback
        public uint Id;

        public override int Size => base.Size + Signature.Length + sizeof(uint);

        public Commit() : base(ConsensusMessageType.Commit) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadFixedBytes(64);
            Id = reader.ReadUInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
            writer.Write(Id);
        }
    }
}
