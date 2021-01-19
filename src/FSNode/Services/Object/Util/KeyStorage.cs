using Neo.FSNode.Services.Session.Storage;
using NeoFS.API.v2.Session;
using System;

namespace Neo.FSNode.Services.Object.Util
{
    public class KeyStorage
    {
        private byte[] key;
        private TokenStore tokenStore;

        public KeyStorage(byte[] localKey, TokenStore ts)
        {
            this.key = localKey;
            this.tokenStore = ts;
        }

        public byte[] GetKey(SessionToken token)
        {
            if (token != null)
            {
                var pToken = this.tokenStore.Get(token.Body.OwnerId, token.Body.Id.ToByteArray());
                if (pToken == null)
                    throw new ArgumentException("private token not found, could not get session key");
                return pToken.SessionKey;
            }
            return this.key;
        }
    }
}
