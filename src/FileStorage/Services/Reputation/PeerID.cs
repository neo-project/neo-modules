using System;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Reputaion
{
    public class PeerID
    {
        public const int PeerIDLength = 33;
        private readonly byte[] value;

        public PeerID(byte[] bytes)
        {
            if (bytes is null || bytes.Length != PeerIDLength)
                throw new ArgumentException("invalid PeerID bytes", nameof(bytes));
            value = bytes;
        }

        public static PeerID FromString(string idString)
        {
            return new PeerID(StrictUTF8.GetBytes(idString));
        }

        public byte[] ToByteArray()
        {
            return value;
        }

        public override string ToString()
        {
            return StrictUTF8.GetString(value);
        }
    }
}
