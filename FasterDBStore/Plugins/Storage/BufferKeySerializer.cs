using FASTER.core;

namespace Neo.Plugins.Storage
{
    internal class BufferKeySerializer : BinaryObjectSerializer<BufferKey>
    {
        public override void Serialize(ref BufferKey key)
        {
            writer.Write(key.Table);
            writer.Write(key.Key.Length);
            writer.Write(key.Key);
        }

        public override void Deserialize(ref BufferKey key)
        {
            key.Table = reader.ReadByte();
            var length = reader.ReadInt32();
            key.Key = reader.ReadBytes(length);
        }
    }
}
