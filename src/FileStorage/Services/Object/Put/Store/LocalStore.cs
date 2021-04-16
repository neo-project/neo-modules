using Neo.FileStorage.API.Refs;
using FSObject = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.LocalObjectStorage.Engine;

namespace Neo.FileStorage.Services.Object.Put.Store
{
    public class LocalStore : IStore
    {
        private readonly StorageEngine localStorage;
        public void Put(FSObject obj)
        {
            localStorage.Put(obj);
        }
    }
}
