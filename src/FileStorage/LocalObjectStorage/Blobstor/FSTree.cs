using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.LocalObjectStorage.Blobstor
{
    public class FSTree
    {
        public const int DefaultDirNameLength = 1;
        public const int MaxDepth = (32 - 1) / DefaultDirNameLength;
        public const int DefaultShallowDepth = 4;
        public const string DefaultRootPath = "./Data_FSTree";

        public int Depth { get; init; }
        public int DirNameLen { get; init; }
        public string RootPath { get; init; }

        public FSTree()
        {
            Depth = DefaultShallowDepth;
            DirNameLen = DefaultDirNameLength * 2;
            RootPath = DefaultRootPath;
        }

        public FSTree(string root, int d, int l)
        {
            RootPath = root;
            Depth = MaxDepth < d ? MaxDepth : d;
            DirNameLen = l * 2;
        }

        private string StringifyAddress(Address address)
        {
            return address.String().Replace('/', '.');
        }

        private string TreePath(Address address)
        {
            string saddr = StringifyAddress(address);
            List<string> dirs = new() { RootPath };
            for (int i = 0; i < Depth; i++)
            {
                dirs.Add(saddr[..DirNameLen]);
                saddr = saddr[DirNameLen..];
            }
            dirs.Add(saddr);
            return Path.Join(dirs.ToArray());
        }

        public void Iterate(Action<Address, byte[]> handler)
        {
            Iterate(0, new string[] { RootPath }, handler);
        }

        private void Iterate(int depth, IEnumerable<string> paths, Action<Address, byte[]> handler)
        {
            var dirs = Directory.GetDirectories(Path.Join(paths.ToArray())).Select(p => new DirectoryInfo(p).Name).ToArray();
            foreach (string dir in dirs)
            {
                var current_paths = paths.Append(dir);
                string current_path = Path.Join(current_paths.ToArray());
                if (depth < Depth + 1)
                {
                    if (!Directory.Exists(current_path)) throw new InvalidOperationException();
                    Iterate(depth + 1, current_paths, handler);
                    continue;
                }
                if (!File.Exists(current_path)) throw new InvalidOperationException();
                Console.WriteLine(current_path);
                Address address = Address.ParseString(string.Join("", current_paths.Skip(1)).Replace('.', '/'));
                byte[] data = File.ReadAllBytes(current_path);
                handler(address, data);
            }
        }

        public string Exists(Address address)
        {
            string path = TreePath(address);
            if (!File.Exists(path)) throw new ObjectFileNotFoundException(nameof(FSTree));
            return path;
        }

        public byte[] Get(Address address)
        {
            string path = Exists(address);
            return File.ReadAllBytes(path);
        }

        public void Put(Address address, byte[] data)
        {
            string path = TreePath(address);
            if (!File.Exists(path))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        public void Delete(Address address)
        {
            string path = Exists(address);
            File.Delete(path);
        }
    }
}
