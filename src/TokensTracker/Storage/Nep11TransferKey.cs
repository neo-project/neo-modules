using Neo.IO;
using Neo.VM.Types;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Neo.Plugins.Storage
{
    public class Nep11TransferKey : TokenTransferKey, IComparable<Nep11TransferKey>, IEquatable<Nep11TransferKey>
    {
        public ByteString Token;
        public override int Size => base.Size + Token.GetVarSize();

        public Nep11TransferKey() : this(new UInt160(), 0, new UInt160(), ByteString.Empty, 0)
        {
        }

        public Nep11TransferKey(UInt160 userScriptHash, ulong timestamp, UInt160 assetScriptHash, ByteString tokenId, uint xferIndex) : base(userScriptHash, timestamp, assetScriptHash, xferIndex)
        {
            Token = tokenId;
        }

        public int CompareTo(Nep11TransferKey other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;
            int result = UserScriptHash.CompareTo(other.UserScriptHash);
            if (result != 0) return result;
            int result2 = TimestampMS.CompareTo(other.TimestampMS);
            if (result2 != 0) return result2;
            int result3 = AssetScriptHash.CompareTo(other.AssetScriptHash);
            if (result3 != 0) return result3;
            var result4 = BlockXferNotificationIndex.CompareTo(other.BlockXferNotificationIndex);
            if (result4 != 0) return result4;
            return (Token.GetInteger() - other.Token.GetInteger()).Sign;
        }

        public bool Equals(Nep11TransferKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserScriptHash.Equals(other.UserScriptHash)
                   && TimestampMS.Equals(other.TimestampMS) && AssetScriptHash.Equals(other.AssetScriptHash)
                   && Token.Equals(other.Token)
                   && BlockXferNotificationIndex.Equals(other.BlockXferNotificationIndex);
        }

        public override bool Equals(Object other)
        {
            return other is Nep11TransferKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = UserScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ TimestampMS.GetHashCode();
                hashCode = (hashCode * 397) ^ AssetScriptHash.GetHashCode();
                hashCode = (hashCode * 397) ^ BlockXferNotificationIndex.GetHashCode();
                hashCode = (hashCode * 397) ^ Token.GetHashCode();
                return hashCode;
            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            try
            {
                base.Serialize(writer);
                writer.WriteVarBytes(Token.GetSpan());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Token = reader.ReadVarBytes();
        }
    }
}
