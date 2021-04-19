
using System;

namespace Neo.FileStorage.Services.Reputaion.Storage
{
    public class UpdatePrm
    {
        public const int PeerIDLength = 33;
        private bool sat;
        private ulong epoch;
        private byte[] peerId;

        public UpdatePrm(byte[] id)
        {
            if (id.Length != PeerIDLength) throw new ArgumentException();
            peerId = id;
        }
    }
}
