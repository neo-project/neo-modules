using Neo.IO;
using System;
using System.IO;

namespace Neo.Consensus
{
    public abstract class ConsensusMessage : ISerializable
    {
        protected readonly byte validatorsCount;
        public readonly ConsensusMessageType Type;
        public uint BlockIndex;
        public byte ValidatorIndex;
        public byte ViewNumber;

        public virtual int Size =>
            sizeof(ConsensusMessageType) +  //Type
            sizeof(uint) +                  //BlockIndex
            sizeof(byte) +                  //ValidatorIndex
            sizeof(byte);                   //ViewNumber

        public ConsensusMessage(byte validatorsCount)
        {
            this.validatorsCount = validatorsCount;
        }

        protected ConsensusMessage(byte validatorsCount, ConsensusMessageType type)
        {
            this.validatorsCount = validatorsCount;
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
            if (ValidatorIndex >= validatorsCount)
                throw new FormatException();
            ViewNumber = reader.ReadByte();
        }

        public static ConsensusMessage DeserializeFrom(byte[] data, byte validatorsCount)
        {
            using MemoryStream ms = new(data, false);
            using BinaryReader reader = new(ms, Utility.StrictUTF8);

            ConsensusMessageType t = (ConsensusMessageType)reader.ReadByte();
            ConsensusMessage message = t switch
            {
                ConsensusMessageType.PrepareRequest => new PrepareRequest(validatorsCount),
                ConsensusMessageType.PrepareResponse => new PrepareResponse(validatorsCount),
                ConsensusMessageType.ChangeView => new ChangeView(validatorsCount),
                ConsensusMessageType.Commit => new Commit(validatorsCount),
                ConsensusMessageType.RecoveryRequest => new RecoveryRequest(validatorsCount),
                ConsensusMessageType.RecoveryMessage => new RecoveryMessage(validatorsCount),
                _ => throw new FormatException(),
            };
            message.Deserialize(reader);
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
