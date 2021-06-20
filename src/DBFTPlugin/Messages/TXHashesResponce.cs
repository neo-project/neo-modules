using System;
using Neo.IO;
using System.IO;
using System.Linq;

namespace Neo.Consensus.Messages
{
    public class TXHashesResponce : ConsensusMessage
    {
        public byte[] Signature;
        public UInt256[] TransactionHashes;

        public override int Size => base.Size
            + Signature.Length                  
            + TransactionHashes.GetVarSize();   //TransactionHashes;

        public TXHashesResponce() : base(ConsensusMessageType.TXHashesResponce) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadFixedBytes(64);
            TransactionHashes = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (TransactionHashes.Distinct().Count() != TransactionHashes.Length)
                throw new FormatException();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
            writer.Write(TransactionHashes);
        }
    }
}
