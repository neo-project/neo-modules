using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IClientKeyRemoteProvider
    {
        IWriterProvider WithClient(IFSClient client);
    }
}
