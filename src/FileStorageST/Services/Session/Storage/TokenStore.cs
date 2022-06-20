using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Database;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Concurrent;
using static Neo.Helper;

namespace Neo.FileStorage.Storage.Services.Session.Storage
{
    public class TokenStore : ITokenStorage
    {
        public const string DefaultSessionStorePath = "./Data_Session";
        private readonly ConcurrentDictionary<string, PrivateToken> tokens = new();
        private readonly IDB db;

        public TokenStore(IDB db)
        {
            this.db = db;
        }

        public PrivateToken Get(OwnerID owner, byte[] token)
        {
            var key = StoreKey(owner, token);
            var skey = Convert.ToBase64String(key);
            if (tokens.TryGetValue(skey, out var p))
                return p;
            var raw = db.Get(key);
            if (raw is not null)
            {
                p = raw.AsSerializable<PrivateToken>();
                tokens.TryAdd(skey, p);
                return p;
            }
            return null;
        }

        public CreateResponse.Types.Body Create(CreateRequest request)
        {
            var gb = Guid.NewGuid().ToByteArray();
            var key = StoreKey(request.Body.OwnerId, gb);
            var skey = Convert.ToBase64String(key);
            var sk = new byte[32];
            var random = new Random();
            random.NextBytes(sk);
            tokens[skey] = new PrivateToken
            {
                SessionKey = sk.LoadPrivateKey(),
                Expiration = request.Body.Expiration,
            };
            db.Put(key, tokens[skey].ToArray());
            var keyPair = new KeyPair(sk);
            return new CreateResponse.Types.Body()
            {
                Id = ByteString.CopyFrom(gb),
                SessionKey = ByteString.CopyFrom(keyPair.PublicKey.EncodePoint(true))
            };
        }

        private byte[] StoreKey(OwnerID owner, byte[] token)
        {
            return Concat(owner.Value.ToByteArray(), token);
        }

        public void RemoveExpired(ulong epoch)
        {
            foreach (var (key, token) in tokens)
            {
                if (token.Expiration <= epoch)
                    if (!tokens.TryRemove(key, out _))
                        Utility.Log(nameof(TokenStore), LogLevel.Debug, $"could not remove expired session token, try next epoch");
            }
            db.Iterate(Array.Empty<byte>(), (key, value) =>
            {
                var p = value.AsSerializable<PrivateToken>();
                if (p.Expiration <= epoch) db.Delete(key);
                return false;
            });
        }
    }
}
