using NeoFS.API.v2.Refs;
using FsObject = NeoFS.API.v2.Object.Object;

namespace Neo.Fs.LocalObjectStorage.LocalStore
{
    public static class Helper
    {
        public static Address Address(this FsObject fsObject)
        {
            return new Address()
            {
                ObjectId = fsObject.ObjectId,
                ContainerId = fsObject.Header.ContainerId
            };
        }

        public static FsObject CutPayload(this FsObject fsObject)
        {
            fsObject.Payload = null;
            return fsObject;
        }
    }
}
