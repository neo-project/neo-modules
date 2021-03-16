using Neo.FileStorage.API.Refs;
using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.LocalStore
{
    public static class Helper
    {
        public static Address Address(this V2Object obj)
        {
            return new Address()
            {
                ObjectId = obj.ObjectId,
                ContainerId = obj.Header.ContainerId
            };
        }

        public static V2Object CutPayload(this V2Object obj)
        {
            obj.Payload = null;
            return obj;
        }
    }
}
