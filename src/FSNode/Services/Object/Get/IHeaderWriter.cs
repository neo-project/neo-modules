using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Get
{
    public interface IHeaderWriter
    {
        void WriteHeader(V2Object obj);
    }
}
