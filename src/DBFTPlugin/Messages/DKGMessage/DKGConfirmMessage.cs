using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class DKGConfirmMessage : ConsensusMessage
    {
        public override int Size => base.Size;

        public DKGConfirmMessage() : base(ConsensusMessageType.DKGConfirmMessage) { }

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
