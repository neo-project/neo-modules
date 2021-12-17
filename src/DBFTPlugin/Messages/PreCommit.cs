using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class PreCommit : ConsensusMessage
    {
        public UInt256 PreparationHash;

        // priority or fallback
        public uint Id;
        public override int Size => base.Size + PreparationHash.Size;

        public PreCommit() : base(ConsensusMessageType.PreCommit) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            PreparationHash = reader.ReadSerializable<UInt256>();
            Id = reader.ReadUInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(PreparationHash);
            writer.Write(Id);
        }
    }
}
