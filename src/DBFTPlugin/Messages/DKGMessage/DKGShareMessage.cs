using System;
using Neo.IO;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Neo.Consensus
{
    public class DKGShareMessage : ConsensusMessage
    {

        public byte[][] keys;

        public override int Size => base.Size + keys.Length;

        public DKGShareMessage() : base(ConsensusMessageType.DKGShareMessage) { }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);

            List<byte[]> enckeys = new List<byte[]>();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var key = reader.ReadBytes(60);
                enckeys.Add(key);
            }

            keys = enckeys.ToArray();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            foreach (var key in keys)
                writer.Write(key);
        }
    }
}
