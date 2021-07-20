using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common
{
    public interface IWriter
    {
        void Write(PeerToPeerTrust trust);

        void Close();
    }
}
