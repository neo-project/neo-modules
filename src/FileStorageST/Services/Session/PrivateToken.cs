using System.Security.Cryptography;

namespace Neo.FileStorage.Storage.Services.Session
{
    public class PrivateToken
    {
        public ECDsa SessionKey;
        public ulong Expiration;
    }
}
