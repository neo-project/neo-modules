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
    public class WriteBatch
    {
        internal readonly IntPtr Handle = Native.leveldb_writebatch_create();

        ~WriteBatch()
        {
            Native.leveldb_writebatch_destroy(Handle);
        }

        public void Clear()
        {
            Native.leveldb_writebatch_clear(Handle);
        }

        public void Delete(byte[] key)
        {
            Native.leveldb_writebatch_delete(Handle, key, (UIntPtr)key.Length);
        }

        public void Put(byte[] key, byte[] value)
        {
            Native.leveldb_writebatch_put(Handle, key, (UIntPtr)key.Length, value, (UIntPtr)value.Length);
        }
    }
}
