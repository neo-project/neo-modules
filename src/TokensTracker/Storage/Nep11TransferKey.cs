using Neo.IO;
using Neo.VM.Types;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Neo.Plugins.Storage
{
    public class Nep11TransferKey : IComparable<Nep11TransferKey>, IEquatable<Nep11TransferKey>, ISerializable
    {
        public readonly UInt160 UserScriptHash;
        public ulong TimestampMS { get; private set; }
        public readonly UInt160 AssetScriptHash;
        public ByteString Token;

        public uint BlockXferNotificationIndex { get; private set; }

        public int Size =>
            UInt160.Length +    //UserScriptHash
            sizeof(ulong) +     //TimestampMS
            UInt160.Length +    //AssetScriptHash
            Token.GetVarSize() +
            sizeof(uint);     //BlockXferNotificationIndex

        public Nep11TransferKey() : this(new UInt160(), 0, new UInt160(), ByteString.Empty, 0)
        {
        }

        public Nep11TransferKey(UInt160 userScriptHash, ulong timestamp, UInt160 assetScriptHash, ByteString tokenId, uint xferIndex)
        {
            if (userScriptHash is null || assetScriptHash is null || tokenId == null)
                throw new ArgumentNullException();
            UserScriptHash = userScriptHash;
            TimestampMS = timestamp;
            AssetScriptHash = assetScriptHash;
            Token = tokenId;
            BlockXferNotificationIndex = xferIndex;
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

        public void Serialize(BinaryWriter writer)
        {
            try
            {
                writer.Write(UserScriptHash);
                if (BitConverter.IsLittleEndian) writer.Write(BinaryPrimitives.ReverseEndianness(TimestampMS));
                else writer.Write(TimestampMS);
                writer.Write(AssetScriptHash);
                writer.WriteVarBytes(Token.GetSpan());
                writer.Write(BlockXferNotificationIndex);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            ((ISerializable)UserScriptHash).Deserialize(reader);
            if (BitConverter.IsLittleEndian) TimestampMS = BinaryPrimitives.ReverseEndianness(reader.ReadUInt64());
            else TimestampMS = reader.ReadUInt64();
            ((ISerializable)AssetScriptHash).Deserialize(reader);
            Token = reader.ReadVarBytes();

            BlockXferNotificationIndex = reader.ReadUInt16();
        }
    }
}
