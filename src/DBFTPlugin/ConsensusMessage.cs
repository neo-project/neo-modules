using Neo.IO;
using System;
using System.IO;

namespace Neo.Consensus
{
    public abstract class ConsensusMessage : ISerializable
    {
        public readonly ConsensusMessageType Type;
        public uint BlockIndex;
        public byte ValidatorIndex;
        public byte ViewNumber;

        public virtual int Size =>
            sizeof(ConsensusMessageType) +  //Type
            sizeof(uint) +                  //BlockIndex
            sizeof(byte) +                  //ValidatorIndex
            sizeof(byte);                   //ViewNumber

        protected ConsensusMessage(ConsensusMessageType type)
        {
            if (!Enum.IsDefined(typeof(ConsensusMessageType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            this.Type = type;
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            if (Type != (ConsensusMessageType)reader.ReadByte())
                throw new FormatException();
            BlockIndex = reader.ReadUInt32();
            ValidatorIndex = reader.ReadByte();
            ViewNumber = reader.ReadByte();
        }

        public static ConsensusMessage DeserializeFrom(byte[] data, byte validatorsCount)
        {
            using MemoryStream ms = new(data, false);
            using BinaryReader reader = new(ms, Utility.StrictUTF8);

            ConsensusMessageType t = (ConsensusMessageType)data[0];
            ConsensusMessage message = t switch
            {
                ConsensusMessageType.PrepareRequest => new PrepareRequest(),
                ConsensusMessageType.PrepareResponse => new PrepareResponse(),
                ConsensusMessageType.ChangeView => new ChangeView(),
                ConsensusMessageType.Commit => new Commit(),
                ConsensusMessageType.RecoveryRequest => new RecoveryRequest(),
                ConsensusMessageType.RecoveryMessage => new RecoveryMessage(validatorsCount),
                _ => throw new FormatException(),
            };
            message.Deserialize(reader);
            if (message.ValidatorIndex >= validatorsCount)
                throw new FormatException();
            return message;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(BlockIndex);
            writer.Write(ValidatorIndex);
            writer.Write(ViewNumber);
        }
    }
}
