using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class DKGTestMessage : ConsensusMessage
    {

        public override int Size => base.Size;

        public DKGTestMessage() : base(ConsensusMessageType.DKGTest) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
        }
    }
}
