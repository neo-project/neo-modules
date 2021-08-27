using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public interface ILocalObjectStore
    {
        void Put(FSObject obj);
    }
}
