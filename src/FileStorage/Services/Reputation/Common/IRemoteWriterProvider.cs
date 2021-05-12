using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IRemoteWriterProvider
    {
        IWriterProvider InitRemote(NodeInfo ni);
    }
}
