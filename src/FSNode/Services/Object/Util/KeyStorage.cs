using Neo.FSNode.Services.Session.Storage;
using NeoFS.API.v2.Session;
using NeoFS.API.v2.Cryptography;
using System;
using System.Security.Cryptography;

namespace Neo.FSNode.Services.Object.Util
{
    public class KeyStorage
    {
        private ECDsa key;
        private TokenStore tokenStore;

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
