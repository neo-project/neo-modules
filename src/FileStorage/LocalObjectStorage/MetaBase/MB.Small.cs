using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.IO.Data.LevelDB;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public BlobovniczaID IsSmall(Address address)
        {
            return db.Get(ReadOptions.Default, SmallKey(address));
        }
    }
}
