using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Database;
using Neo.FileStorage.Database.LevelDB;
using System;
using System.IO;
using System.Text;
using System.Threading;

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
            LoadUsedSpace();
        }

        public void Dispose()
        {
            dB?.Dispose();
        }

        private void LoadUsedSpace()
        {
            dB.Iterate(Array.Empty<byte>(), (key, value) =>
            {
                IncSize(value.Length);
                return false;
            });
        }

        private byte[] Addresskey(Address address)
        {
            return Encoding.UTF8.GetBytes(address.String());
        }

        public byte[] Get(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            var raw = dB.Get(Addresskey(address));
            if (raw is null) throw new ObjectNotFoundException();
            return raw;
        }

        public void Put(Address address, byte[] data)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            if (data is null || data.Length == 0) throw new ArgumentException("invalid " + nameof(data));
            if (ObjSizeLimit < (ulong)data.Length) throw new SizeExceedLimitException();
            if (FullSizeLimit < (ulong)(filled + data.Length)) throw new BlobFullException();
            var key = Addresskey(address);
            dB.Put(key, data);
            IncSize(data.Length);
        }

        public void Delete(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
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

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is Blobovnicza blob) return Equals(blob);
            return false;
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
