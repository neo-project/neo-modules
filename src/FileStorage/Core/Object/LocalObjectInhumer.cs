using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;

namespace Neo.FileStorage.Core.Object
{
    public class LocalObjectInhumer : IObjectDeleteHandler
    {
        public StorageEngine LocalStorage { get; init; }

        public void DeleteObjects(params Address[] addresses)
        {
            for (int i = 1; i < addresses.Length; i++)
            {
                LocalStorage.Inhume(addresses[0], addresses[i]);
            }
        }
    }
}
