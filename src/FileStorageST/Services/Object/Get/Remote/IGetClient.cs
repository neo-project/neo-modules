using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public interface IGetClient : IObjectGetClient
    {
        IRawObjectGetClient RawObjectGetClient();
    }
}
