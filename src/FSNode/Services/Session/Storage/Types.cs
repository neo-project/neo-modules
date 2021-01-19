namespace Neo.FSNode.Services.Session.Storage
{
    public class PrivateToken
    {
        private byte[] sessionKey;
        private ulong exp;

        public byte[] SessionKey
        {
            get => this.sessionKey;
        }

        public PrivateToken(byte[] sk, ulong expiration)
        {
            this.sessionKey = sk;
            this.exp = expiration;
        }
    }
}
