using Neo.Persistence;

namespace Neo.Storage.RocksDB
{
    // TODO: Delete this file when https://github.com/neo-project/neo/pull/1087 was merged

    public interface IStoragePlugin
    {
        Store GetStore();
    }
}
