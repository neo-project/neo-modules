using Neo.IO;
using System;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    abstract class StateMessage : ISerializable
    {
        public readonly MessageType Type;

        public virtual int Size => sizeof(MessageType);

        protected StateMessage(MessageType type)
        {
            this.Type = type;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            if (Type != (MessageType)reader.ReadByte())
                throw new FormatException();
        }
    }
}
