// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.Storage.LevelDBStore is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.IO.Data.LevelDB
{
    public class ReadOptions
    {
        public static readonly ReadOptions Default = new ReadOptions();
        internal readonly IntPtr Handle = Native.leveldb_readoptions_create();

        public bool VerifyChecksums
        {
            set
            {
                Native.leveldb_readoptions_set_verify_checksums(Handle, value);
            }
        }

        public bool FillCache
        {
            set
            {
                Native.leveldb_readoptions_set_fill_cache(Handle, value);
            }
        }

        public Snapshot Snapshot
        {
            set
            {
                Native.leveldb_readoptions_set_snapshot(Handle, value.handle);
            }
        }

        ~ReadOptions()
        {
            Native.leveldb_readoptions_destroy(Handle);
        }
    }
}
