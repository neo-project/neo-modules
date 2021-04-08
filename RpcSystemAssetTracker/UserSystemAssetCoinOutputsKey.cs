using System;
using System.Collections.Generic;
using System.IO;
using Cron.IO;

namespace Cron.Plugins
{
    public class UserSystemAssetCoinOutputsKey : IComparable<UserSystemAssetCoinOutputsKey>, IEquatable<UserSystemAssetCoinOutputsKey>,
        ISerializable
    {
        public byte IdToken; // It's either the governing token or the utility token
        public readonly UInt160 UserAddress;
        public readonly UInt256 TxHash;

        public int Size => 1 + UserAddress.Size + TxHash.Size;

        public bool Equals(UserSystemAssetCoinOutputsKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return (IdToken == other.IdToken) && Equals(UserAddress, other.UserAddress) && Equals(TxHash, other.TxHash);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is UserSystemAssetCoinOutputsKey b)) return false;
            return Equals(b);
        }

        public int CompareTo(UserSystemAssetCoinOutputsKey other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var isGoverningTokenComparison = IdToken.CompareTo(other.IdToken);
            if (isGoverningTokenComparison != 0) return isGoverningTokenComparison;
            var userAddressComparison = Comparer<UInt160>.Default.Compare(UserAddress, other.UserAddress);
            if (userAddressComparison != 0) return userAddressComparison;
            return Comparer<UInt256>.Default.Compare(TxHash, other.TxHash);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IdToken.GetHashCode();
                hashCode = (hashCode * 397) ^ (UserAddress != null ? UserAddress.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TxHash != null ? TxHash.GetHashCode() : 0);
                return hashCode;
            }
        }

        public UserSystemAssetCoinOutputsKey()
        {
            UserAddress = new UInt160();
            TxHash = new UInt256();
        }

        public UserSystemAssetCoinOutputsKey(byte idToken, UInt160 userAddress, UInt256 txHash)
        {
            IdToken = idToken;
            UserAddress = userAddress;
            TxHash = txHash;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(IdToken);
            writer.Write(UserAddress.ToArray());
            writer.Write(TxHash.ToArray());
        }

        public void Deserialize(BinaryReader reader)
        {
            IdToken = reader.ReadByte();
            ((ISerializable)UserAddress).Deserialize(reader);
            ((ISerializable)TxHash).Deserialize(reader);
        }
    }
}