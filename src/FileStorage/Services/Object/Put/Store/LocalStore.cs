using Neo.FileStorage.API.Refs;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.LocalObjectStorage.LocalStore;

namespace Neo.FileStorage.Services.Object.Put.Store
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
