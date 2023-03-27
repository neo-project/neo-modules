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
using System.IO;

namespace Neo.IO.Data.LevelDB
{
    public class DB : IDisposable
    {
        private IntPtr _handle;

        /// <summary>
        /// Return true if haven't got valid handle
        /// </summary>
        public bool IsDisposed => _handle == IntPtr.Zero;

        private DB(IntPtr handle)
        {
            this._handle = handle;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Native.leveldb_close(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public void Delete(WriteOptions options, byte[] key)
        {
            Native.leveldb_delete(_handle, options.Handle, key, (UIntPtr)key.Length, out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public byte[] Get(ReadOptions options, byte[] key)
        {
            IntPtr value = Native.leveldb_get(_handle, options.Handle, key, (UIntPtr)key.Length, out UIntPtr length, out IntPtr error);
            try
            {
                NativeHelper.CheckError(error);
                return value.ToByteArray(length);
            }
            finally
            {
                if (value != IntPtr.Zero) Native.leveldb_free(value);
            }
        }

        public bool Contains(ReadOptions options, byte[] key)
        {
            IntPtr value = Native.leveldb_get(_handle, options.Handle, key, (UIntPtr)key.Length, out _, out IntPtr error);
            NativeHelper.CheckError(error);

            if (value != IntPtr.Zero)
            {
                Native.leveldb_free(value);
                return true;
            }

            return false;
        }

        public Snapshot GetSnapshot()
        {
            return new Snapshot(_handle);
        }

        public Iterator NewIterator(ReadOptions options)
        {
            return new Iterator(Native.leveldb_create_iterator(_handle, options.Handle));
        }

        public static DB Open(string name)
        {
            return Open(name, Options.Default);
        }

        public static DB Open(string name, Options options)
        {
            IntPtr handle = Native.leveldb_open(options.Handle, Path.GetFullPath(name), out IntPtr error);
            NativeHelper.CheckError(error);
            return new DB(handle);
        }

        public void Put(WriteOptions options, byte[] key, byte[] value)
        {
            Native.leveldb_put(_handle, options.Handle, key, (UIntPtr)key.Length, value, (UIntPtr)value.Length, out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public static void Repair(string name, Options options)
        {
            Native.leveldb_repair_db(options.Handle, Path.GetFullPath(name), out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public void Write(WriteOptions options, WriteBatch writeBatch)
        {
            Native.leveldb_write(_handle, options.Handle, writeBatch.Handle, out IntPtr error);
            NativeHelper.CheckError(error);
        }
    }
}
