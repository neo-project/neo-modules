using FASTER.core;
using Neo.IO;

namespace Neo.Plugins.Storage
{
    internal class BufferValueSerializer : BinaryObjectSerializer<BufferValue>
    {
        public override void Serialize(ref BufferValue value)
        {
            writer.WriteVarBytes(value.Value);
        }

        public override void Deserialize(ref BufferValue value)
        {
            value.Value = reader.ReadVarBytes(BufferKeySerializer.MaxLength);
        }
    }
}
