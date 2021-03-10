using Neo.IO;
using System;
using System.IO;

namespace Neo.Consensus
{
    partial class RecoveryMessage
    {
        public class PreparationPayloadCompact : ISerializable
        {
            private readonly byte validatorsCount;
            public byte ValidatorIndex;
            public byte[] InvocationScript;

            int ISerializable.Size =>
                sizeof(byte) +                  //ValidatorIndex
                InvocationScript.GetVarSize();  //InvocationScript

            public PreparationPayloadCompact(byte validatorsCount)
            {
                this.validatorsCount = validatorsCount;
            }

            public void Deserialize(BinaryReader reader)
            {
                ValidatorIndex = reader.ReadByte();
                if (ValidatorIndex >= validatorsCount)
                    throw new FormatException();
                InvocationScript = reader.ReadVarBytes(1024);
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(ValidatorIndex);
                writer.WriteVarBytes(InvocationScript);
            }
        }
    }
}
