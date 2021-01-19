using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.LocalObjectStorage.LocalStore;

namespace Neo.FSNode.Services.Object.Put.Store
{
    public class LocalStore : IStore
    {
        private readonly Storage localStorage;
        public void Put(V2Object obj)
        {
            localStorage.Put(obj);
        }
    }
}
