using FASTER.core;

namespace Neo.Plugins.Storage.Plugins.Storage
{
    internal class BufferValueSerializer : BinaryObjectSerializer<BufferValue>
    {
        public override void Serialize(ref BufferValue value)
        {
            writer.Write(value.Value.Length);
            writer.Write(value.Value);
        }

        public override void Deserialize(ref BufferValue value)
        {
            var length = reader.ReadInt32();
            value.Value = reader.ReadBytes(length);
        }
    }
}
