using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class IterationTrust
    {
        public ulong Epoch;
        public uint Index;
        public PeerToPeerTrust Trust;
    }
}
