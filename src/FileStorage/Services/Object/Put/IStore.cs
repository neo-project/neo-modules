using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Put
{
    public interface IStore
    {
        void Put(V2Object obj);
    }
}
