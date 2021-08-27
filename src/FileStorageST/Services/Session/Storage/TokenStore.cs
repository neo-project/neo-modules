using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.Wallets;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Session.Storage
{
    public class TokenStore : ITokenStorage
    {
        private class PrivateToken
        {
            public ECDsa SessionKey;
            public ulong Expiration;
        }

        private readonly ConcurrentDictionary<string, PrivateToken> tokens = new();

        public ECDsa Get(OwnerID owner, byte[] token)
        {
            ECDsa key = null;
            if (tokens.TryGetValue(StoreKey(owner, token), out var p))
                key = p.SessionKey;
            return key;
        }

        public CreateResponse.Types.Body Create(CreateRequest request)
        {
            var gb = Guid.NewGuid().ToByteArray();
            var key = StoreKey(request.Body.OwnerId, gb);
            var sk = new byte[32];
            var random = new Random();
            random.NextBytes(sk);
            tokens[key] = new PrivateToken
            {
                SessionKey = sk.LoadPrivateKey(),
                Expiration = request.Body.Expiration,
            };
            var keyPair = new KeyPair(sk);
            return new CreateResponse.Types.Body()
            {
                Id = ByteString.CopyFrom(gb),
                SessionKey = ByteString.CopyFrom(keyPair.PublicKey.EncodePoint(true))
            };
        }

        private string StoreKey(OwnerID owner, byte[] token)
        {
            return Convert.ToBase64String(owner.Value.ToByteArray()) + Convert.ToBase64String(token);
        }
    }
}
