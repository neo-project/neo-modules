using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Database;
using Neo.FileStorage.Database.LevelDB;
using System;
using System.IO;
using System.Text;
using System.Threading;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blob
{
    public sealed class Blobovnicza : IEquatable<Blobovnicza>, IDisposable
    {
        public const ulong DefaultFullSizeLimit = 1 << 30;
        public const ulong DefaultObjSizeLimit = 1 << 20;
        public readonly Func<byte[], byte[]> DefaultCompressor = d => d;
        public string Path { get; private set; }
        public ulong FullSizeLimit { get; init; }
        public ulong ObjSizeLimit { get; init; }
        private IDB dB;
        private long filled;

        public Blobovnicza(string path)
        {
            Path = path;
            FullSizeLimit = DefaultFullSizeLimit;
            ObjSizeLimit = DefaultObjSizeLimit;
        }

        public void Open()
        {
            var full = System.IO.Path.GetFullPath(Path);
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
            dB = new DB(full);
        }

        public void Dispose()
        {
            dB?.Dispose();
        }

        private byte[] Addresskey(Address address)
        {
            return Encoding.UTF8.GetBytes(address.String());
        }

        public byte[] Get(Address address)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));
            var raw = dB.Get(Addresskey(address));
            if (raw is null) throw new ObjectNotFoundException();
            return raw;
        }

        public void Put(Address address, byte[] data)
        {
            if (address is null || data is null)
                throw new ArgumentNullException();
            if (FullSizeLimit < (ulong)filled) throw new BlobFullException();
            if (ObjSizeLimit < (ulong)data.Length)
                throw new SizeExceedLimitException();
            var key = Addresskey(address);
            dB.Put(key, data);
            IncSize(data.Length);
        }

        public void Delete(Address address)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));
            var raw = dB.Get(Addresskey(address));
            if (raw is null) throw new ObjectNotFoundException();
            dB.Delete(Addresskey(address));
            DecSize(raw.Length);
        }

        private void IncSize(long size)
        {
            Interlocked.Add(ref filled, size);
        }

        private void DecSize(long size)
        {
            Interlocked.Add(ref filled, -size);
        }

        public bool Equals(Blobovnicza other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Path == other.Path) return true;
            return false;
        }
    }
}
