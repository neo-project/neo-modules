using Neo.IO;
using System;
using System.IO;

namespace Neo.Consensus
{
    partial class RecoveryMessage
    {
        public class CommitPayloadCompact : ISerializable
        {
            private readonly byte validatorsCount;
            public byte ViewNumber;
            public byte ValidatorIndex;
            public byte[] Signature;
            public byte[] InvocationScript;

            int ISerializable.Size =>
                sizeof(byte) +                  //ViewNumber
                sizeof(byte) +                  //ValidatorIndex
                Signature.Length +              //Signature
                InvocationScript.GetVarSize();  //InvocationScript

            public CommitPayloadCompact(byte validatorsCount)
            {
                this.validatorsCount = validatorsCount;
            }

            public void Deserialize(BinaryReader reader)
            {
                ViewNumber = reader.ReadByte();
                ValidatorIndex = reader.ReadByte();
                if (ValidatorIndex >= validatorsCount)
                    throw new FormatException();
                Signature = reader.ReadFixedBytes(64);
                InvocationScript = reader.ReadVarBytes(1024);
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(ViewNumber);
                writer.Write(ValidatorIndex);
                writer.Write(Signature);
                writer.WriteVarBytes(InvocationScript);
            }
        }
    }
}
