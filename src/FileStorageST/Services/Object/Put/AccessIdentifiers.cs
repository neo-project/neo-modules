using Neo.FileStorage.API.Refs;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public class AccessIdentifiers
    {
        public ObjectID Parent;
        public ObjectID Self;
        public FSObject ParentHeader;
    }
}
