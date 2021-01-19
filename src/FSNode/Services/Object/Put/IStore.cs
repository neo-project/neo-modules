using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Put
{
    public interface IStore
    {
        void Put(V2Object obj);
    }
}
