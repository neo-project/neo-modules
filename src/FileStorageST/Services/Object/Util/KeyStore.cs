using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Services.Session;
using System;
using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class KeyStore
    {
        public const string SessionTokenInexistentError = "session token does not exist";
        public const string SessionTokenExpiratedError = "session token has been expired";

        private readonly ECDsa key;
        private readonly ITokenStorage tokenStore;
        private readonly IEpochSource epochSource;

        public KeyStore(ECDsa localKey, ITokenStorage ts, IEpochSource es)
        {
            key = localKey;
            tokenStore = ts;
            epochSource = es;
        }

        public ECDsa GetKey(SessionToken token)
        {
            if (token != null)
            {
                var pt = tokenStore.Get(token.Body.OwnerId, token.Body.Id.ToByteArray());
                if (pt is not null)
                    if (pt.Expiration < epochSource.CurrentEpoch)
                        throw new InvalidOperationException(SessionTokenExpiratedError);
                    else
                        return pt.SessionKey;
                throw new InvalidOperationException(SessionTokenInexistentError);
            }
            return key;
        }
    }
}
