using System;
using System.IO;
using Neo.IO;

namespace Neo.Plugins
{
    public class Nep17BalanceKey : IComparable<Nep17BalanceKey>, IEquatable<Nep17BalanceKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public readonly UInt160 AssetScriptHash;

        public int Size => 20 + 20;

        public Nep17BalanceKey() : this(new UInt160(), new UInt160())
        {
        }

        public Nep17BalanceKey(UInt160 userScriptHash, UInt160 assetScriptHash)
        {
            if (userScriptHash == null || assetScriptHash == null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            AssetScriptHash = assetScriptHash;
        }

        public int CompareTo(Nep17BalanceKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            int result = UserScriptHash.CompareTo(other.UserScriptHash);
            if (result != 0) return result;
            return AssetScriptHash.CompareTo(other.AssetScriptHash);
        }

        public bool Equals(Nep17BalanceKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserScriptHash.Equals(other.UserScriptHash) && AssetScriptHash.Equals(AssetScriptHash);
        }

        public override bool Equals(Object other)
        {
            return other is Nep17BalanceKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UserScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ AssetScriptHash.GetHashCode();
                return hashCode;
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(AssetScriptHash);
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            ((ISerializable)AssetScriptHash).Deserialize(reader);
        }
    }
}
