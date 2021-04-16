using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Put
{
    public interface IStore
    {
        void Put(FSObject obj);
    }
}
