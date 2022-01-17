using System;
using Neo.IO;
using System.IO;
using System.Linq;

namespace Neo.Consensus
{
    public class DKGShareMessage : ConsensusMessage
    {
        public byte[] Signature;

        public byte[] dkgPubKey;

        public UInt256[] keys;
        public override int Size => base.Size + Signature.Length + dkgPubKey.Length + keys.GetVarSize();

        public DKGShareMessage() : base(ConsensusMessageType.DKGShare) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadFixedBytes(64);
            dkgPubKey = reader.ReadBytes(64);
            keys = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (keys.Distinct().Count() != keys.Length)
                throw new FormatException();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
            writer.Write(dkgPubKey);
            writer.Write(keys);
        }
    }
}
