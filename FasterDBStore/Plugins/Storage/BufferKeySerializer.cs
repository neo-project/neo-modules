using FASTER.core;
using Neo.IO;

namespace Neo.Plugins.Storage
{
    internal class BufferKeySerializer : BinaryObjectSerializer<BufferKey>
    {
        internal const int MaxLength = 1024 * 1024;

        public override void Serialize(ref BufferKey key)
        {
            writer.Write(key.Table);
            writer.WriteVarInt(key.Key.Length);
            writer.Write(key.Key);
        }

        public override void Deserialize(ref BufferKey key)
        {
            key.Table = reader.ReadByte();
            var length = (int)reader.ReadVarInt(MaxLength);
            key.Key = reader.ReadBytes(length);
        }
    }
}
