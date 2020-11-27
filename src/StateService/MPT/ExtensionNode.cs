using Neo.IO;
using Neo.SmartContract;
using System.IO;

namespace Neo.Plugins.MPT
{
    public class ExtensionNode : MPTNode
    {
        //max lenght when store StorageKey
        public const int MaxKeyLength = (ApplicationEngine.MaxStorageValueSize + sizeof(int)) * 2;

        public byte[] Key;
        public MPTNode Next;

        protected override NodeType Type => NodeType.ExtensionNode;

        internal override void EncodeSpecific(BinaryWriter writer)
        {
            writer.WriteVarBytes(Key);
            WriteHash(writer, Next.Hash);
        }

        internal override void DecodeSpecific(BinaryReader reader)
        {
            Key = reader.ReadVarBytes(MaxKeyLength);
            Next = new HashNode();
            Next.DecodeSpecific(reader);
        }
    }
}
