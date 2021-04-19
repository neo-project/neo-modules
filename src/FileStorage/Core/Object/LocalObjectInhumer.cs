using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;

namespace Neo.FileStorage.Core.Object
{
    public class LocalObjectInhumer : IObjectDeleteHandler
    {
        public StorageEngine LocalStorage { get; init; }

        public void DeleteObjects(params Address[] ids)
        {
            for (int i = 1; i < ids.Length; i++)
            {
                LocalStorage.Inhume(ids[0], ids[i]);
            }
        }
    }
}
