// Copyright (C) 2015-2021 The Neo Project.
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
using LSnapshot = Neo.IO.Data.LevelDB.Snapshot;

namespace Neo.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly DB db;
        private readonly LSnapshot snapshot;
        private readonly ReadOptions options;
        private readonly WriteBatch batch;

        public Snapshot(DB db)
        {
            this.db = db;
            this.snapshot = db.GetSnapshot();
            this.options = new ReadOptions { FillCache = false, Snapshot = snapshot };
            this.batch = new WriteBatch();
        }

        public void Commit()
        {
            db.Write(WriteOptions.Default, batch);
        }

        public void Delete(byte[] key)
        {
            batch.Delete(key);
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] prefix, SeekDirection direction = SeekDirection.Forward)
        {
            return db.Seek(options, prefix, direction, (k, v) => (k, v));
        }

        public void Put(byte[] key, byte[] value)
        {
            batch.Put(key, value);
        }

        public bool Contains(byte[] key)
        {
            return db.Contains(options, key);
        }

        public byte[] TryGet(byte[] key)
        {
            return db.Get(options, key);
        }
    }
}
