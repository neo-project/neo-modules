using Neo.IO;
using System;
using System.IO;

namespace Neo.Plugins
{
    public class Nep17TransferKey : IComparable<Nep17TransferKey>, IEquatable<Nep17TransferKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public ulong TimestampMS { get; private set; }
        public readonly UInt160 AssetScriptHash;
        public ushort BlockXferNotificationIndex { get; private set; }

        public int Size =>
            UInt160.Length +    //UserScriptHash
            sizeof(ulong) +     //TimestampMS
            UInt160.Length +    //AssetScriptHash
            sizeof(ushort);     //BlockXferNotificationIndex

        public Nep17TransferKey() : this(new UInt160(), 0, new UInt160(), 0)
        {
        }

        public Nep17TransferKey(UInt160 userScriptHash, ulong timestamp, UInt160 assetScriptHash, ushort xferIndex)
        {
            if (userScriptHash is null || assetScriptHash is null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            TimestampMS = timestamp;
            AssetScriptHash = assetScriptHash;
            BlockXferNotificationIndex = xferIndex;
        }

        public int CompareTo(Nep17TransferKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            int result = UserScriptHash.CompareTo(other.UserScriptHash);
            if (result != 0) return result;
            int result2 = TimestampMS.CompareTo(other.TimestampMS);
            if (result2 != 0) return result2;
            int result3 = AssetScriptHash.CompareTo(other.AssetScriptHash);
            if (result3 != 0) return result3;
            return BlockXferNotificationIndex.CompareTo(other.BlockXferNotificationIndex);
        }

        public bool Equals(Nep17TransferKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserScriptHash.Equals(other.UserScriptHash)
                   && TimestampMS.Equals(other.TimestampMS) && AssetScriptHash.Equals(other.AssetScriptHash)
                   && BlockXferNotificationIndex.Equals(other.BlockXferNotificationIndex);
        }

        public override bool Equals(Object other)
        {
            return other is Nep17TransferKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UserScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ TimestampMS.GetHashCode();
                hashCode = (hashCode * 397) ^ AssetScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ BlockXferNotificationIndex.GetHashCode();
                return hashCode;
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            var timestampBytes = BitConverter.GetBytes(TimestampMS);
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            writer.Write(timestampBytes);
            writer.Write(AssetScriptHash);
            writer.Write(BlockXferNotificationIndex);
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            byte[] timestampBytes = new byte[sizeof(ulong)];
            reader.Read(timestampBytes, 0, timestampBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            TimestampMS = BitConverter.ToUInt64(timestampBytes, 0);
            ((ISerializable)AssetScriptHash).Deserialize(reader);
            BlockXferNotificationIndex = reader.ReadUInt16();
        }
    }
}
