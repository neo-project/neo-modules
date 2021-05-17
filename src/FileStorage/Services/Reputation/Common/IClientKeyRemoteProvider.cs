using APIClient = Neo.FileStorage.API.Client.Client;

namespace Neo.FileStorage.Services.Reputaion.Common
{
    public interface IClientKeyRemoteProvider
    {
        IWriterProvider WithClient(APIClient client);
    }
}
