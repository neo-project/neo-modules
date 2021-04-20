using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;

namespace Neo.FileStorage.Core.Object
{
    public class LocalObjectRemover : IObjectDeleteHandler
    {
        public StorageEngine LocalStorage { get; init; }

        public void DeleteObjects(params Address[] addresses)
        {
            LocalStorage.Delete(addresses);
        }
    }
}
