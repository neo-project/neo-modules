using RocksDbSharp;

namespace Neo.Plugins.Storage
{
    public static class Options
    {
        public static readonly DbOptions Default = (DbOptions)new DbOptions()
            .SetCreateMissingColumnFamilies(true)
            .SetCreateIfMissing(true)
            .SetErrorIfExists(false)
            .SetMaxOpenFiles(1000)
            .SetParanoidChecks(false)
            .SetWriteBufferSize(4 << 20)
            .SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(4096));
        public static readonly ReadOptions ReadDefault = new ReadOptions();
        public static readonly WriteOptions WriteDefault = new WriteOptions();
        public static readonly WriteOptions WriteDefaultSync = new WriteOptions()
            .SetSync(true);
    }
}
