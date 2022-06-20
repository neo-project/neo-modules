using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Core.Object
{
    public interface IObjectLocker
    {
        void Lock(ContainerID cid, ObjectID locker, params ObjectID[] locked);
    }
}
