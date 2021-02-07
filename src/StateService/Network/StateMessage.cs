using Neo.IO;
using System;
using System.IO;

namespace Neo.Plugins.StateService.Network
{
    public class StateMessage : ISerializable
    {
        public MessageType Type;
        public ISerializable Payload;

        public int Size => sizeof(MessageType) + Payload.Size;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Payload);
        }

        public void Deserialize(BinaryReader reader)
        {
            Type = (MessageType)reader.ReadByte();
            Payload = Type switch
            {
                MessageType.StateRoot => reader.ReadSerializable<StateRoot>(),
                MessageType.Vote => reader.ReadSerializable<Vote>(),
                _ => throw new FormatException(),
            };
        }

        public static StateMessage CreateStateRootMessage(StateRoot state_root)
        {
            return new StateMessage
            {
                Type = MessageType.StateRoot,
                Payload = state_root,
            };
        }

        public static StateMessage CreateVoteMessage(Vote vote)
        {
            return new StateMessage
            {
                Type = MessageType.Vote,
                Payload = vote,
            };
        }
    }
}
