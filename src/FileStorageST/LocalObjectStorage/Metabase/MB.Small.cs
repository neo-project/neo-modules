using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public bool IsSmall(Address address, out BlobovniczaID bid)
        {
            bid = null;
            var data = db.Get(SmallKey(address));
            if (data is null) return false;
            bid = data;
            return true;
        }
    }
}
