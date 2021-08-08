using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public interface IGetClient : IObjectClient, IRawObjectClient
    {
        IRawObjectClient Raw()
        {
            return this;
        }
    }
}
