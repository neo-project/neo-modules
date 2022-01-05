using Neo.FileStorage.API.Client;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public interface IPutClient : IObjectPutClient
    {
        IRawObjectPutClient RawObjectPutClient();
    }
}
