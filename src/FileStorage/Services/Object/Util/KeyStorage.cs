using System;
using System.Security.Cryptography;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Services.Session.Storage;

namespace Neo.FileStorage.Services.Object.Util
{
    public class KeyStorage
    {
        private readonly ECDsa key;
        private readonly TokenStore tokenStore;

        public KeyStorage(ECDsa localKey, TokenStore ts)
        {
            key = localKey;
            tokenStore = ts;
        }

        public ECDsa GetKey(SessionToken token)
        {
            if (token != null)
            {
                var pToken = tokenStore.Get(token.Body.OwnerId, token.Body.Id.ToByteArray());
                if (pToken == null)
                    throw new ArgumentException("private token not found, could not get session key");
                return pToken.SessionKey;
            }
            return key;
        }
    }
}
