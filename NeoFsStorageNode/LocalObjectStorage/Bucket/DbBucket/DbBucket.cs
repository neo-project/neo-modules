using Neo.IO.Data.LevelDB;
using System;
using System.Linq;
using System.Text;
using LevelDbOptions = Neo.IO.Data.LevelDB.Options;

namespace Neo.Fs.LocalObjectStorage.Bucket
{
    public class DbBucket:IBucket
    {
        private readonly DB db;
        public byte[] Name { get; set; }

        // TBD
        public DbBucket(Options opts)
        {
            this.db = DB.Open(opts.Path, opts.LevelDbOptions);
        }

        public byte[] Get(byte[] key)
        {
            return db.Get(ReadOptions.Default, key);
        }

        public void Set(byte[] key, byte[] value)
        {
            db.Put(WriteOptions.Default, key, value);
        }

        public void Del(byte[] key)
        {
            db.Delete(WriteOptions.Default, key);
        }

        public bool Has(byte[] key)
        {
            var r = db.Get(ReadOptions.Default, key);
            return !(r is null) && r.Length != 0;
        }

        public long Size()
        {
            //throw new NotImplementedException();
            long size = 0;
            ReadOptions opt = new ReadOptions { FillCache = false };
            using (Iterator it = db.NewIterator(opt))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    size += it.Value().Length;
                }
            }
            return size;
        }

        public byte[][] List()
        {
            byte[][] r = new byte[][] { };
            ReadOptions opt = new ReadOptions { FillCache = false };
            using (Iterator it = db.NewIterator(opt))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    r = r.Append(it.Value()).ToArray();
                }
            }
            return r;
        }

        public void Iterate(FilterHandler handler)
        {
            if (handler is null)
                throw new ArgumentException("handler cannot be null");
            ReadOptions opt = new ReadOptions { FillCache = false };
            using (Iterator it = db.NewIterator(opt))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    if (!handler(it.Key(), it.Value()))
                        throw new Exception("iteration aborted");
                }
            }
        }

        public void Close()
        {
            db.Dispose();
        }
    }

    public class Options
    {
        public LevelDbOptions LevelDbOptions { get; set; }
        public byte[] Name { get; set; }
        public string Path { get; set; }
        //public uint Perm { get; set; } // FileMode

        public Options(string path, LevelDbOptions levelDbOptions)
        {
            this.Name = Encoding.ASCII.GetBytes("leveldb");
            this.Path = path;
            //this.Perm = 0777;
            this.LevelDbOptions = levelDbOptions;
        }
    }
}
