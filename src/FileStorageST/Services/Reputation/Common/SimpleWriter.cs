using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public class NonWriter : IWriter
    {
        public void Write(PeerToPeerTrust _) { }

        public void Close() { }
    }
}
