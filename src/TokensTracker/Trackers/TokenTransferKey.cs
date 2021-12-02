using System;
using System.Buffers.Binary;
using System.IO;
using Neo.IO;

namespace Neo.Plugins.Trackers
{
    public class TokenTransferKey : ISerializable
    {
        public UInt160 UserScriptHash { get; protected set; }
        public ulong TimestampMS { get; protected set; }
        public UInt160 AssetScriptHash { get; protected set; }
        public uint BlockXferNotificationIndex { get; protected set; }

        public TokenTransferKey(UInt160 userScriptHash, ulong timestamp, UInt160 assetScriptHash, uint xferIndex)
        {
            if (userScriptHash is null || assetScriptHash is null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            TimestampMS = timestamp;
            AssetScriptHash = assetScriptHash;
            BlockXferNotificationIndex = xferIndex;
        }
        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(TimestampMS) : TimestampMS);
            writer.Write(AssetScriptHash);
            writer.Write(BlockXferNotificationIndex);
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            UserScriptHash.Deserialize(reader);
            TimestampMS = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(reader.ReadUInt64()) : reader.ReadUInt64();
            AssetScriptHash.Deserialize(reader);
            BlockXferNotificationIndex = reader.ReadUInt32();
        }

        public virtual int Size =>
              UInt160.Length +    //UserScriptHash
              sizeof(ulong) +     //TimestampMS
              UInt160.Length +    //AssetScriptHash
              sizeof(uint);       //BlockXferNotificationIndex
    }
}
