
using System.IO;


namespace Neo.Consensus.Messages
{
    public class TXHashesRequest : ConsensusMessage
    {

        public override int Size => base.Size;

        public TXHashesRequest() : base(ConsensusMessageType.TXHashesRequest) { }

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
