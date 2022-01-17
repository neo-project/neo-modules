using Neo.IO;
using System.IO;

namespace Neo.Consensus
{
    public class DKGConfirmMessage : ConsensusMessage
    {
        public byte[] Signature;

        public override int Size => base.Size + Signature.Length;

        public DKGConfirmMessage() : base(ConsensusMessageType.DKGConfirm) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadFixedBytes(64);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
        }
    }
}
