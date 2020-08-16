using System;
using System.IO;

namespace Neo.IO.Data.LevelDB
{
    public class DB : IDisposable
    {
        private IntPtr handle;

        /// <summary>
        /// Return true if haven't got valid handle
        /// </summary>
        public bool IsDisposed => handle == IntPtr.Zero;

        private DB(IntPtr handle)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Native.leveldb_close(handle);
                handle = IntPtr.Zero;
            }
        }

        public void Delete(WriteOptions options, byte[] key)
        {
            Native.leveldb_delete(handle, options.handle, key, (UIntPtr)key.Length, out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public byte[] Get(ReadOptions options, byte[] key)
        {
            IntPtr value = Native.leveldb_get(handle, options.handle, key, (UIntPtr)key.Length, out UIntPtr length, out IntPtr error);
            try
            {
                if (error != IntPtr.Zero)
                {
                    Native.leveldb_free(error);
                    return null;
                }
                return value.ToByteArray(length);
            }
            finally
            {
                if (value != IntPtr.Zero) Native.leveldb_free(value);
            }
        }

        public Snapshot GetSnapshot()
        {
            return new Snapshot(handle);
        }

        public Iterator NewIterator(ReadOptions options)
        {
            return new Iterator(Native.leveldb_create_iterator(handle, options.handle));
        }

        public static DB Open(string name)
        {
            return Open(name, Options.Default);
        }

        public static DB Open(string name, Options options)
        {
            IntPtr handle = Native.leveldb_open(options.handle, Path.GetFullPath(name), out IntPtr error);
            NativeHelper.CheckError(error);
            return new DB(handle);
        }

        public void Put(WriteOptions options, byte[] key, byte[] value)
        {
            Native.leveldb_put(handle, options.handle, key, (UIntPtr)key.Length, value, (UIntPtr)value.Length, out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public static void Repair(string name, Options options)
        {
            Native.leveldb_repair_db(options.handle, Path.GetFullPath(name), out IntPtr error);
            NativeHelper.CheckError(error);
        }

        public void Write(WriteOptions options, WriteBatch write_batch)
        {
            // There's a bug in .Net Core.
            // When calling DB.Write(), it will throw LevelDBException sometimes.
            // But when you try to catch the exception, the bug disappears.
            // We shall remove the "try...catch" clause when Microsoft fix the bug.
            byte retry = 0;
            while (true)
            {
                try
                {
                    Native.leveldb_write(handle, options.handle, write_batch.handle, out IntPtr error);
                    NativeHelper.CheckError(error);
                    break;
                }
                catch (LevelDBException ex)
                {
                    if (++retry >= 4) throw;
                    System.IO.File.AppendAllText("leveldb.log", ex.Message + "\r\n");
                }
            }
        }
    }
}
