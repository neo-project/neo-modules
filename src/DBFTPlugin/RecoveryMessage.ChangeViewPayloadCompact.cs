using Neo.IO;
using System;
using System.IO;

namespace Neo.Consensus
{
    partial class RecoveryMessage
    {
        public class ChangeViewPayloadCompact : ISerializable
        {
            private readonly byte validatorsCount;
            public byte ValidatorIndex;
            public byte OriginalViewNumber;
            public ulong Timestamp;
            public byte[] InvocationScript;

            int ISerializable.Size =>
                sizeof(byte) +                  //ValidatorIndex
                sizeof(byte) +                  //OriginalViewNumber
                sizeof(ulong) +                 //Timestamp
                InvocationScript.GetVarSize();  //InvocationScript

            public ChangeViewPayloadCompact(byte validatorsCount)
            {
                this.validatorsCount = validatorsCount;
            }

            public void Deserialize(BinaryReader reader)
            {
                ValidatorIndex = reader.ReadByte();
                if (ValidatorIndex >= validatorsCount)
                    throw new FormatException();
                OriginalViewNumber = reader.ReadByte();
                Timestamp = reader.ReadUInt64();
                InvocationScript = reader.ReadVarBytes(1024);
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(ValidatorIndex);
                writer.Write(OriginalViewNumber);
                writer.Write(Timestamp);
                writer.WriteVarBytes(InvocationScript);
            }
        }
    }
}
