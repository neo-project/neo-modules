using System;
using Neo.IO;
using System.IO;
using System.Linq;

namespace Neo.Consensus
{
    public class TxListMessage : ConsensusMessage
    {
        public UInt256[] TransactionHashes;

        public override int Size => base.Size
            + TransactionHashes.GetVarSize();   //TransactionHashes;

        public TxListMessage(ConsensusMessageType type) : base(type) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            TransactionHashes = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (TransactionHashes.Distinct().Count() != TransactionHashes.Length)
                throw new FormatException();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(TransactionHashes);
        }
    }
}
