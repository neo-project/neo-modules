using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Fs.LocalObjectStorage.Bucket
{
    public class TreeBucket : IBucket
    {
        public string Dir { get; set; }
        public uint Perm { get; set; } // FileMode

        public int Depth { get; set; }
        public int PrefixLength { get; set; }
        public long Sz { get; set; }

        public string StringifyHexKey(byte[] key)
        {
            return key.ToHexString();
        }

        public byte[] DecodeHexKey(string key)
        {
            return key.HexToBytes();
        }

        // TreePath returns slice of the dir names that contain the path
        // and filename, e.g. 0xabcdef => []string{"ab", "cd"}, "abcdef".
        // In case of errors - return nil slice.
        public (string[], string) TreePath(byte[] key)
        {
            var fileName = StringifyHexKey(key);
            if (fileName.Length <= this.PrefixLength * this.Depth)
                return (null, fileName);

            var filePath = fileName;
            var dirs = new string[0];
            for (int i = 0; i < this.Depth; i++)
            {
                dirs = dirs.Append(filePath.Substring(0, this.PrefixLength)).ToArray();
                filePath = filePath.Substring(this.PrefixLength, filePath.Length - this.PrefixLength);
            }

            return (dirs, fileName);
        }

        public byte[] Get(byte[] key)
        {
            var (dirPaths, fileName) = this.TreePath(key);
            if (dirPaths == null)
                throw new ArgumentException("key is too short for tree fs bucket");
            var p = Path.Combine(this.Dir, Path.Combine(dirPaths), fileName);
            if (!File.Exists(p))
                throw new ArgumentException("key not found");
            return File.ReadAllBytes(p);
        }

        public void Set(byte[] key, byte[] value)
        {
            var (dirPaths, fileName) = this.TreePath(key);
            if (dirPaths == null)
                throw new ArgumentException("key is too short for tree fs bucket");
            var dirPath = Path.Combine(dirPaths);
            var p = Path.Combine(this.Dir, dirPath, fileName);
            Directory.CreateDirectory(Path.Combine(this.Dir, dirPath));
            File.WriteAllBytes(p, value);
            var info = new FileInfo(p);
            this.Sz += info.Length;
        }

        public void Del(byte[] key)
        {
            var (dirPaths, fileName) = this.TreePath(key);
            if (dirPaths == null)
                throw new ArgumentException("key is too short for tree fs bucket");
            var dirPath = Path.Combine(dirPaths);
            var p = Path.Combine(this.Dir, dirPath, fileName);
            if (!File.Exists(p))
                throw new ArgumentException("key not found");
            var info = new FileInfo(p);
            this.Sz -= info.Length;
            File.Delete(p);
        }

        public bool Has(byte[] key)
        {
            var (dirPaths, fileName) = this.TreePath(key);
            if (dirPaths == null)
                return false;

            var p = Path.Combine(this.Dir, Path.Combine(dirPaths), fileName);
            return File.Exists(p);
        }

        private delegate void Fn(string path); // process file 
        private const int QueueCap = 1000;

        private void Listing(string root, Fn fn)
        {
            var q = new Queue<Element>(QueueCap);
            q.Enqueue(new Element() { Path = root });

            while (q.Count > 0)
            {
                var e = q.Dequeue();
                if (File.Exists(e.Path))
                {
                    var info = new FileInfo(e.Path);
                    if (e.Depth == this.Depth + 1 && info.Name.StartsWith(e.Prefix))
                        fn(e.Path);
                    continue;
                }

                DirectoryInfo directoryInfo = new DirectoryInfo(e.Path);
                if (e.Depth > this.Depth || (e.Depth > 0 && directoryInfo.Name.Length > this.PrefixLength))
                    continue;


                string[] allPaths = Directory.GetDirectories(e.Path).Concat(Directory.GetFiles(e.Path)).ToArray();

                foreach (var p in allPaths)
                {
                    // add prefix of all dirs in path except root dir
                    string prefix = "";
                    if (e.Depth > 0)
                        prefix = e.Prefix + directoryInfo.Name;

                    q.Enqueue(new Element()
                    {
                        Depth = e.Depth + 1,
                        Prefix = prefix,
                        Path = Path.Combine(e.Path, p)
                    });
                }
            }
        }

        public long Size()
        {
            return this.Sz;
        }

        // TBD
        private long Size2()
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
                buckets = buckets.Append(DecodeHexKey(info.Name)).ToArray();
            };
            Listing(this.Dir, fn);
            return buckets;
        }

        public void Iterate(FilterHandler handler)
        {
            if (handler is null)
                throw new ArgumentException("handler cannot be null");
            Fn fn = p =>
            {
                FileInfo info = new FileInfo(p);
                var key = DecodeHexKey(info.Name);
                var value = File.ReadAllBytes(p);
                if (!handler(key, value))
                    throw new Exception("iteration aborted");
            };
            Listing(this.Dir, fn);
        }

        public void Close()
        {
            //Fn fn = p =>
            //{
            //    File.Delete(p);
            //};
            //Listing(this.Dir, fn);
            Directory.Delete(Dir, true);
        }
    }

    class Element
    {
        public int Depth { get; set; }
        public string Prefix { get; set; }
        public string Path { get; set; }
    }
}
