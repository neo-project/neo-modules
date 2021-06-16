using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;

namespace Neo.FileStorage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public BlobovniczaID IsSmall(Address address)
        {
            return db.Get(SmallKey(address));
        }
    }
}
