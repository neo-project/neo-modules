using System;
using Neo.IO;
using System.IO;
namespace Neo.Consensus
{
    partial class PrepareRequest
    {
        public class TXListPayloadCompact : ISerializable
        {
            public byte ValidatorIndex;
            public byte OriginalViewNumber;
            public ulong Timestamp;
            public byte[] InvocationScript;

            public byte[] TransactionHashes;

            int ISerializable.Size =>
                sizeof(byte) +                  //ValidatorIndex
                sizeof(byte) +                  //OriginalViewNumber
                sizeof(ulong) +                 //Timestamp
                sizeof(ushort) +                // InvocationScriptSize
                InvocationScript.GetVarSize() + //InvocationScript
                sizeof(ushort) +                // TXList size
                TransactionHashes.GetVarSize(); // TXList

            void ISerializable.Deserialize(BinaryReader reader)
            {
                ValidatorIndex = reader.ReadByte();
                OriginalViewNumber = reader.ReadByte();
                Timestamp = reader.ReadUInt64();
                InvocationScript = reader.ReadVarBytes(1024);
                TransactionHashes = reader.ReadVarBytes();

                if (TransactionHashes.Length % 32 != 0) throw new FormatException("Wrong TransactionHashes format");
            }

            void ISerializable.Serialize(BinaryWriter writer)
            {
                writer.Write(ValidatorIndex);
                writer.Write(OriginalViewNumber);
                writer.Write(Timestamp);
                writer.Write((ushort)InvocationScript.GetVarSize());
                writer.WriteVarBytes(InvocationScript);
                writer.Write((ushort)TransactionHashes.GetVarSize());
                writer.WriteVarBytes(TransactionHashes);
            }
        }
    }
}
