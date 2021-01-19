using NeoFS.API.v2.Refs;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.LocalObjectStorage.LocalStore
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
