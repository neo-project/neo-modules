using System;
using System.IO;
using Neo.IO;

namespace Neo.Plugins
{
    public class Nep5TransferKey : IComparable<Nep5TransferKey>, IEquatable<Nep5TransferKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public uint Timestamp { get; private set; }
        public readonly UInt160 AssetScriptHash;
        public ushort BlockXferNotificationIndex { get; private set; }

        public int Size => 20 + sizeof(uint) + 20 + sizeof(ushort);

        public Nep5TransferKey() : this(new UInt160(), 0, new UInt160(), 0)
        {
        }

        public Nep5TransferKey(UInt160 userScriptHash, uint timestamp, UInt160 assetScriptHash, ushort xferIndex)
        {
            if (userScriptHash is null || assetScriptHash is null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            Timestamp = timestamp;
            AssetScriptHash = assetScriptHash;
            BlockXferNotificationIndex = xferIndex;
        }

        public int CompareTo(Nep5TransferKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            int result = UserScriptHash.CompareTo(other.UserScriptHash);
            if (result != 0) return result;
            int result2 = Timestamp.CompareTo(other.Timestamp);
            if (result2 != 0) return result2;
            int result3 = AssetScriptHash.CompareTo(other.AssetScriptHash);
            if (result3 != 0) return result3;
            return BlockXferNotificationIndex.CompareTo(other.BlockXferNotificationIndex);
        }

        public bool Equals(Nep5TransferKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserScriptHash.Equals(other.UserScriptHash)
                   && Timestamp.Equals(other.Timestamp) && AssetScriptHash.Equals(other.AssetScriptHash)
                   && BlockXferNotificationIndex.Equals(other.BlockXferNotificationIndex);
        }

        public override bool Equals(Object other)
        {
            return other is Nep5TransferKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UserScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ Timestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ AssetScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ BlockXferNotificationIndex.GetHashCode();
                return hashCode;
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            var timestampBytes = BitConverter.GetBytes(Timestamp);
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            writer.Write(timestampBytes);
            writer.Write(AssetScriptHash);
            writer.Write(BlockXferNotificationIndex);
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            byte[] timestampBytes = new byte[sizeof(uint)];
            reader.Read(timestampBytes, 0, sizeof(uint));
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            Timestamp = BitConverter.ToUInt32(timestampBytes, 0);
            ((ISerializable)AssetScriptHash).Deserialize(reader);
            BlockXferNotificationIndex = reader.ReadUInt16();
        }
    }
}