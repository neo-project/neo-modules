using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public class FSTree
    {
        public const int DefaultDirNameLength = 1;
        public const int MaxDepth = (32 - 1) / DefaultDirNameLength;
        public const int DefaultShallowDepth = 4;
        public const string DefaultPath = "Data_FSTree";

        private readonly int depth;
        private readonly int dirNameLen;
        private readonly string rootPath;

        public FSTree(string root, int d, int dir_len)
        {
            depth = d > MaxDepth ? MaxDepth : d;
            if (dir_len == 0 || depth * dir_len >= 32) dir_len = DefaultDirNameLength;
            dirNameLen = dir_len * 2;
            rootPath = root;
            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);
        }

        private string StringifyAddress(Address address)
        {
            return address.String().Replace('/', '.');
        }

        private string TreePath(Address address)
        {
            string saddr = StringifyAddress(address);
            List<string> dirs = new() { rootPath };
            for (int i = 0; i < depth; i++)
            {
                dirs.Add(saddr[..dirNameLen]);
                saddr = saddr[dirNameLen..];
            }
            dirs.Add(saddr);
            return Path.Join(dirs.ToArray());
        }

        public void Iterate(Action<Address, byte[]> handler)
        {
            Iterate(0, new string[] { rootPath }, handler);
        }

        private void Iterate(int d, IEnumerable<string> paths, Action<Address, byte[]> handler)
        {
            string[] dirs;
            if (d == depth)
                dirs = Directory.GetFiles(Path.Join(paths.ToArray())).Select(p => new FileInfo(p).Name).ToArray();
            else
                dirs = Directory.GetDirectories(Path.Join(paths.ToArray())).Select(p => new DirectoryInfo(p).Name).ToArray();
            foreach (string dir in dirs)
            {
                var current_paths = paths.Append(dir);
                string current_path = Path.Join(current_paths.ToArray());
                if (d == depth)
                {
                    if (!File.Exists(current_path)) throw new InvalidOperationException();
                    Address address = Address.FromString(string.Join("", current_paths.Skip(1)).Replace('.', '/'));
                    byte[] data = File.ReadAllBytes(current_path);
                    handler(address, data);
                }
                if (d < depth)
                {
                    if (!Directory.Exists(current_path)) throw new InvalidOperationException();
                    Iterate(d + 1, current_paths, handler);
                    continue;
                }
            }
        }

        public string Exists(Address address)
        {
            string path = TreePath(address);
            if (!File.Exists(path)) throw new ObjectNotFoundException();
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
            Utility.Log(nameof(FSTree), LogLevel.Debug, $"fstree put object, path={path}, address={address.String()}");
            File.WriteAllBytes(path, data);
        }

        public void Delete(Address address)
        {
            string path = Exists(address);
            File.Delete(path);
        }
    }
}
