using Neo.Persistence;

namespace Neo.Storage.RocksDB
{
    // TODO: Delete me on https://github.com/neo-project/neo/pull/1087

    public interface IStoragePlugin
    {
        Store GetStore();
    }
}
