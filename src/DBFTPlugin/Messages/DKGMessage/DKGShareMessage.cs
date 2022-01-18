using System;
using Neo.IO;
using System.IO;
using System.Linq;

namespace Neo.Consensus
{
    public class DKGShareMessage : ConsensusMessage
    {
        public UInt256[] keys;
        public override int Size => base.Size + keys.GetVarSize();

        public DKGShareMessage() : base(ConsensusMessageType.DKGShare) {}

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            keys = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (keys.Distinct().Count() != keys.Length)
                throw new FormatException();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(keys);
        }
    }
}
