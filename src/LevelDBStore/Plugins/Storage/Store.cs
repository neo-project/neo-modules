// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.Storage.LevelDBStore is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        private readonly DB _db;

        public Store(string path)
        {
            this._db = DB.Open(path, new Options { CreateIfMissing = true, FilterPolicy = Native.leveldb_filterpolicy_create_bloom(15) });
        }

        public void Delete(byte[] key)
        {
            _db.Delete(WriteOptions.Default, key);
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        public IEnumerable<(byte[], byte[])> Seek(byte[] prefix, SeekDirection direction = SeekDirection.Forward)
        {
            return _db.Seek(ReadOptions.Default, prefix, direction, (k, v) => (k, v));
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(_db);
        }

        public void Put(byte[] key, byte[] value)
        {
            _db.Put(WriteOptions.Default, key, value);
        }

        public void PutSync(byte[] key, byte[] value)
        {
            _db.Put(WriteOptions.SyncWrite, key, value);
        }

        public bool Contains(byte[] key)
        {
            return _db.Contains(ReadOptions.Default, key);
        }

        public byte[] TryGet(byte[] key)
        {
            return _db.Get(ReadOptions.Default, key);
        }
    }
}
