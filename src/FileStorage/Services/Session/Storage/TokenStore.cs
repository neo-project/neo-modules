using Google.Protobuf;
using Neo.Cryptography;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Concurrent;

namespace Neo.FileStorage.Services.Session.Storage
{
    public class TokenStore
    {
        private readonly ConcurrentDictionary<Key, PrivateToken> tokens = new();

        public PrivateToken Get(OwnerID owner, byte[] token)
        {
            var b = owner.ToByteArray();
            var k = new Key(token, b);
            return tokens[k];
        }

        public CreateResponse.Types.Body Create(CreateRequest request)
        {
            var b = request.Body.OwnerId.ToByteArray();
            var gb = Guid.NewGuid().ToByteArray();
            var key = new Key(gb, b);
            var sk = new byte[64];
            var random = new Random();
            random.NextBytes(sk);
            tokens[key] = new PrivateToken(sk.LoadPrivateKey(), request.Body.Expiration);
            var keyPair = new KeyPair(sk);
            return new CreateResponse.Types.Body()
            {
                Id = ByteString.CopyFrom(gb),
                SessionKey = ByteString.CopyFrom(keyPair.PublicKey.EncodePoint(true))
            };
        }
    }
}
