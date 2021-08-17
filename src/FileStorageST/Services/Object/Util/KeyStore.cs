using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Session;
using Neo.FileStorage.Storage.Services.Session.Storage;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class KeyStore
    {
        private readonly ECDsa key;
        private readonly ITokenStorage tokenStore;

        public KeyStore(ECDsa localKey, ITokenStorage ts)
        {
            key = localKey;
            tokenStore = ts;
        }

        public ECDsa GetKey(SessionToken token)
        {
            if (token != null)
            {
                var key = tokenStore.Get(token.Body.OwnerId, token.Body.Id.ToByteArray());
                if (key is null)
                    throw new KeyNotFoundException("private token not found, could not get session key");
                return key;
            }
            return key;
        }
    }
}
