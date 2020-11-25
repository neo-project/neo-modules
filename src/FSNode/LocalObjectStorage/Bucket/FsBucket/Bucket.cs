using System;
using System.IO;
using System.Linq;

namespace Neo.Fs.LocalObjectStorage.Bucket
{
    public class Bucket : IBucket
    {
        public string Dir { get; set; }
        public uint Perm { get; set; } // FileMode

        //public Bucket(string prefix, )
        //{

        //}

        public byte[] Get(byte[] key)
        {
            var p = Path.Join(this.Dir, Helper.StringifyKey(key));
            if (!File.Exists(p))
                throw new ArgumentException("key not found");
            return File.ReadAllBytes(p);
        }

        public void Set(byte[] key, byte[] value)
        {
            var p = Path.Join(this.Dir, Helper.StringifyKey(key));
            File.WriteAllBytes(p, value);
        }

        public void Del(byte[] key)
        {
            var p = Path.Join(this.Dir, Helper.StringifyKey(key));
            if (!File.Exists(p))
                throw new ArgumentException("key not found");
            File.Delete(p);
        }

        public bool Has(byte[] key)
        {
            var p = Path.Join(this.Dir, Helper.StringifyKey(key));
            return File.Exists(p);
        }

        private delegate void Fn(string path); // process file 

        private static void Listing(string root, Fn fn)
        {
            if (fn == null)
                return;
            if (File.Exists(root)) // path is a file
            {
                fn(root);
            }
            else if (Directory.Exists(root)) // path is a directory
            {
                string[] files = Directory.GetFiles(root);
                foreach (string file in files)
                    fn(file);

                string[] dirs = Directory.GetDirectories(root);
                foreach (string dir in dirs)
                    Listing(dir, fn);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public long Size()
        {
            long size = 0;
            Fn fn = p =>
            {
                FileInfo info = new FileInfo(p);
                size += info.Length;
            };
            Listing(this.Dir, fn);
            return size;
        }

        public byte[][] List()
        {
            var buckets = new byte[][] { };
            Fn fn = p =>
            {
                FileInfo info = new FileInfo(p);
                buckets = buckets.Append(Helper.DecodeKey(info.Name)).ToArray();
            };
            Listing(this.Dir, fn);
            return buckets;
        }

        public void Iterate(FilterHandler handler)
        {
            Fn fn = p =>
            {
                FileInfo info = new FileInfo(p);
                var key = Helper.DecodeKey(info.Name);
                var value = File.ReadAllBytes(p);
                if (!handler(key, value))
                    throw new Exception("iteration aborted");
            };
            Listing(this.Dir, fn);
        }

        // delete all files in this bucket
        public void Close()
        {
            Directory.Delete(Dir, true);
        }
    }
}
