using System.IO;
using Neo.IO;

namespace Neo.Consensus
{
    public class PreCommit : ConsensusMessage
    {
        public UInt256 PreparationHash;

        // priority or fallback
        public uint PId;
        public override int Size => base.Size + PreparationHash.Size + sizeof(uint);

        public PreCommit() : base(ConsensusMessageType.PreCommit) { }

        public override void Deserialize(ref MemoryReader reader)
        {
            base.Deserialize(ref reader);
            PreparationHash = reader.ReadSerializable<UInt256>();
            PId = reader.ReadUInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(PreparationHash);
            writer.Write(PId);
        }
    }
}
