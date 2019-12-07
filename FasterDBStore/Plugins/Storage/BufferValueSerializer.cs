using FASTER.core;
using Neo.IO;

namespace Neo.Plugins.Storage
{
    internal class BufferValueSerializer : BinaryObjectSerializer<BufferValue>
    {
        public override void Serialize(ref BufferValue value)
        {
            writer.WriteVarInt(value.Value.Length);
            writer.Write(value.Value);
        }

        public override void Deserialize(ref BufferValue value)
        {
            var length = (int)reader.ReadVarInt(BufferKeySerializer.MaxLength);
            value.Value = reader.ReadBytes(length);
        }
    }
}
