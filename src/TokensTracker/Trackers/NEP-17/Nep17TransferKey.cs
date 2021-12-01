using System;
using Neo.IO;

namespace Neo.Plugins.Trackers.NEP_17
{
    public class Nep17TransferKey : TokenTransferKey, IComparable<Nep17TransferKey>, IEquatable<Nep17TransferKey>, ISerializable
    {
        public Nep17TransferKey() : base(new UInt160(), 0, new UInt160(), 0)
        {
        }

        public Nep17TransferKey(UInt160 userScriptHash, ulong timestamp, UInt160 assetScriptHash, uint xferIndex) : base(userScriptHash, timestamp, assetScriptHash, xferIndex)
        {
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
            if (other is null) return false;
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
            return HashCode.Combine(UserScriptHash.GetHashCode(), TimestampMS.GetHashCode(), AssetScriptHash.GetHashCode(), BlockXferNotificationIndex.GetHashCode());
        }
    }
}
