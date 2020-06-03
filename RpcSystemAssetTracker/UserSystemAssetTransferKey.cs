using Neo.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neo.Plugins
{
    public class UserSystemAssetTransferKey : IComparable<UserSystemAssetTransferKey>, IEquatable<UserSystemAssetTransferKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public readonly UInt256 AssetId;
        public uint Timestamp { get; private set; }
        
        public int Size => 20 + 32 + sizeof(uint);

        public UserSystemAssetTransferKey() : this(new UInt160(), new UInt256(), 0)
        {
        }

        public UserSystemAssetTransferKey(UInt160 userScriptHash, UInt256 assetId, uint timestamp)
        {
            UserScriptHash = userScriptHash;
            AssetId = assetId;
            Timestamp = timestamp;
        }

        public int CompareTo(UserSystemAssetTransferKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            var r1 = Comparer<UInt160>.Default.Compare(UserScriptHash, other.UserScriptHash);
            if (r1 != 0) return r1;
            var r2 = Comparer<UInt256>.Default.Compare(AssetId, other.AssetId); 
            if (r2 != 0) return r2;
            return Timestamp.CompareTo(other.Timestamp);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UserScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ AssetId.GetHashCode();
                hashCode = (hashCode * 397) ^ Timestamp.GetHashCode();
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is UserSystemAssetTransferKey b)) return false;
            return Equals(b);
        }

        public bool Equals(UserSystemAssetTransferKey other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(UserScriptHash, other.UserScriptHash)
                && Equals(AssetId, other.AssetId) 
                && Equals(Timestamp, other.Timestamp);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash.ToArray());
            writer.Write(AssetId.ToArray());
            var timestampBytes = BitConverter.GetBytes(Timestamp);
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            writer.Write(timestampBytes);
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            ((ISerializable)AssetId).Deserialize(reader);
            byte[] timestampBytes = new byte[sizeof(uint)];
            reader.Read(timestampBytes, 0, sizeof(uint));
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);
            Timestamp = BitConverter.ToUInt32(timestampBytes, 0);
        }
    }
}
