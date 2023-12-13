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
    public class Iterator : IDisposable
    {
        private IntPtr _handle;

        internal Iterator(IntPtr handle)
        {
            this._handle = handle;
        }

        private void CheckError()
        {
            Native.leveldb_iter_get_error(_handle, out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Native.leveldb_iter_destroy(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public byte[] Key()
        {
            IntPtr key = Native.leveldb_iter_key(_handle, out UIntPtr length);
            CheckError();
            return key.ToByteArray(length);
        }

        public void Next()
        {
            Native.leveldb_iter_next(_handle);
            CheckError();
        }

        public void Prev()
        {
            Native.leveldb_iter_prev(_handle);
            CheckError();
        }

        public void Seek(byte[] target)
        {
            Native.leveldb_iter_seek(_handle, target, (UIntPtr)target.Length);
        }

        public void SeekToFirst()
        {
            Native.leveldb_iter_seek_to_first(_handle);
        }

        public void SeekToLast()
        {
            Native.leveldb_iter_seek_to_last(_handle);
        }

        public bool Valid()
        {
            return Native.leveldb_iter_valid(_handle);
        }

        public byte[] Value()
        {
            IntPtr value = Native.leveldb_iter_value(_handle, out UIntPtr length);
            CheckError();
            return value.ToByteArray(length);
        }
    }
}
