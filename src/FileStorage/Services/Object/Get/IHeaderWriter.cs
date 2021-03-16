using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get
{
    public interface IHeaderWriter
    {
        void WriteHeader(V2Object obj);
    }
}
