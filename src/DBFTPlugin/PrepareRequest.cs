using Neo.IO;
using System;
using System.IO;
using System.Linq;

namespace Neo.Consensus
{
    public class PrepareRequest : ConsensusMessage
    {
        public uint Version;
        public UInt256 PrevHash;
        public ulong Timestamp;
        public byte[] VRFProof;
        public UInt256[] TransactionHashes;

        public override int Size => base.Size
            + sizeof(uint)                      //Version
            + UInt256.Length                    //PrevHash
            + sizeof(ulong)                     //Timestamp
            + 81                                // Nonce
            + TransactionHashes.GetVarSize();   //TransactionHashes

        public PrepareRequest() : base(ConsensusMessageType.PrepareRequest) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Version = reader.ReadUInt32();
            PrevHash = reader.ReadSerializable<UInt256>();
            Timestamp = reader.ReadUInt64();
            VRFProof = reader.ReadBytes(81); // proof size 81 bytes
            TransactionHashes = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (TransactionHashes.Distinct().Count() != TransactionHashes.Length)
                throw new FormatException();
        }

        public override bool Verify(ProtocolSettings protocolSettings)
        {
            if (!base.Verify(protocolSettings)) return false;
            return TransactionHashes.Length <= protocolSettings.MaxTransactionsPerBlock;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Version);
            writer.Write(PrevHash);
            writer.Write(Timestamp);
            writer.Write(VRFProof);
            writer.Write(TransactionHashes);
        }
    }
}
