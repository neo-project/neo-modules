using System;
using System.IO;
using Neo.IO;
using Neo.VM.Types;

namespace Neo.Plugins.Trackers.NEP_11
{
    public class Nep11BalanceKey : IComparable<Nep11BalanceKey>, IEquatable<Nep11BalanceKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public readonly UInt160 AssetScriptHash;
        public ByteString Token;
        public int Size => UInt160.Length + UInt160.Length + Token.GetVarSize();

        public Nep11BalanceKey() : this(new UInt160(), new UInt160(), ByteString.Empty)
        {
        }

        public Nep11BalanceKey(UInt160 userScriptHash, UInt160 assetScriptHash, ByteString tokenId)
        {
            if (userScriptHash == null || assetScriptHash == null || tokenId == null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            AssetScriptHash = assetScriptHash;
            Token = tokenId;
        }

        public int CompareTo(Nep11BalanceKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            int result = UserScriptHash.CompareTo(other.UserScriptHash);
            if (result != 0) return result;
            result = AssetScriptHash.CompareTo(other.AssetScriptHash);
            if (result != 0) return result;
            return (Token.GetInteger() - other.Token.GetInteger()).Sign;
        }

        public bool Equals(Nep11BalanceKey other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserScriptHash.Equals(other.UserScriptHash) && AssetScriptHash.Equals(AssetScriptHash) && Token.Equals(other.Token);
        }

        public override bool Equals(Object other)
        {
            return other is Nep11BalanceKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UserScriptHash.GetHashCode(), AssetScriptHash.GetHashCode(), Token.GetHashCode());
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(UserScriptHash);
            writer.Write(AssetScriptHash);
            writer.WriteVarBytes(Token.GetSpan());
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            ((ISerializable)AssetScriptHash).Deserialize(reader);
            Token = new ByteString(reader.ReadVarBytes());
        }
    }
}
