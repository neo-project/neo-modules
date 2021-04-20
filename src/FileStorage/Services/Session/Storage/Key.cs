using Neo.Cryptography;
using System;

namespace Neo.FileStorage.Services.Session.Storage
{
    public class Key : IEquatable<Key>
    {
        private readonly string tokenId;
        private readonly string ownerId;

        public Key(byte[] tokenId, byte[] ownerId)
        {
            this.tokenId = Base58.Encode(tokenId);
            this.ownerId = Base58.Encode(ownerId);
        }

        bool IEquatable<Key>.Equals(Key other)
        {
            if (other is null) return false;
            return other.tokenId == tokenId && other.ownerId == ownerId;
        }
    }
}
