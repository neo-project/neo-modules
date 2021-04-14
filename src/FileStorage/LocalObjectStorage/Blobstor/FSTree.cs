using Neo.Cryptography;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using System.IO;

namespace Neo.FileStorage.LocalObjectStorage.Blobstor
{
    public class FSTree
    {
        public const int DefaultDirNameLength = 1;
        public const int MaxDepth = (32 - 1) / DefaultDirNameLength;
        public const int defaultShallowDepth = 4;
        public const string DefaultRootPath = "./";

        public int Depth { get; init; }
        public int DirNameLen { get; init; }
        public string RootPath { get; init; }

        public FSTree()
        {
            Depth = defaultShallowDepth;
            DirNameLen = DefaultDirNameLength * 2;
            RootPath = DefaultRootPath;
        }

        public FSTree(string root, int d, int l)
        {
            RootPath = root;
            Depth = d;
            DirNameLen = l * 2;
        }

        private string StringifyAddress(Address address)
        {
            return Utility.StrictUTF8.GetBytes(address.String()).Sha256().ToHexString();
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

        public string Exists(Address address)
        {
            string path = TreePath(address);
            if (!File.Exists(path)) throw new FileNotFoundException(nameof(FSTree));
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
